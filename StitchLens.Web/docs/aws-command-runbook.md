# AWS Command-Level Runbook (us-east-1, x86)

This runbook is the command-level execution order for:
- EC2 `t3.small`
- RDS PostgreSQL `db.t3.micro`
- S3 asset storage
- Nginx + Let's Encrypt TLS

Use placeholders first, then replace with real values.

## 0) Prerequisites

Local machine tools:
- `aws` CLI v2 configured (`aws configure`)
- `jq`
- `ssh`, `scp`

Set variables (copy/paste):

```bash
export AWS_REGION="us-east-1"
export APP_NAME="stitchlens"
export ENV_NAME="prod"
export NAME_PREFIX="${APP_NAME}-${ENV_NAME}"

export DOMAIN_NAME="<your-domain>"
export APP_FQDN="app.${DOMAIN_NAME}"

export MY_IP_CIDR="<your-public-ip>/32"
export KEY_PAIR_NAME="${NAME_PREFIX}-key"
export EC2_INSTANCE_TYPE="t3.small"

export DB_NAME="stitchlens"
export DB_USER="stitchlens_app"
export DB_PASSWORD="<strong-db-password>"
export DB_INSTANCE_CLASS="db.t3.micro"

export S3_ASSETS_BUCKET="${NAME_PREFIX}-assets-<accountid>"
export PARAM_PATH="/${APP_NAME}/${ENV_NAME}"
export ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
```

## 1) Networking and security groups

```bash
# Default VPC for fastest first launch
export VPC_ID=$(aws ec2 describe-vpcs --region "$AWS_REGION" --filters Name=isDefault,Values=true --query 'Vpcs[0].VpcId' --output text)

export WEB_SG_ID=$(aws ec2 create-security-group \
  --region "$AWS_REGION" \
  --group-name "${NAME_PREFIX}-web-sg" \
  --description "${NAME_PREFIX} web security group" \
  --vpc-id "$VPC_ID" \
  --query 'GroupId' --output text)

export DB_SG_ID=$(aws ec2 create-security-group \
  --region "$AWS_REGION" \
  --group-name "${NAME_PREFIX}-db-sg" \
  --description "${NAME_PREFIX} db security group" \
  --vpc-id "$VPC_ID" \
  --query 'GroupId' --output text)

# Web SG ingress
aws ec2 authorize-security-group-ingress --region "$AWS_REGION" --group-id "$WEB_SG_ID" --protocol tcp --port 22  --cidr "$MY_IP_CIDR"
aws ec2 authorize-security-group-ingress --region "$AWS_REGION" --group-id "$WEB_SG_ID" --protocol tcp --port 80  --cidr 0.0.0.0/0
aws ec2 authorize-security-group-ingress --region "$AWS_REGION" --group-id "$WEB_SG_ID" --protocol tcp --port 443 --cidr 0.0.0.0/0

# DB SG ingress from web SG only
aws ec2 authorize-security-group-ingress --region "$AWS_REGION" --group-id "$DB_SG_ID" --protocol tcp --port 5432 --source-group "$WEB_SG_ID"
```

## 2) Key pair and EC2

```bash
aws ec2 create-key-pair \
  --region "$AWS_REGION" \
  --key-name "$KEY_PAIR_NAME" \
  --query 'KeyMaterial' --output text > "${KEY_PAIR_NAME}.pem"

chmod 400 "${KEY_PAIR_NAME}.pem"

export AMI_ID=$(aws ssm get-parameter \
  --region "$AWS_REGION" \
  --name "/aws/service/canonical/ubuntu/server/24.04/stable/current/amd64/hvm/ebs-gp3/ami-id" \
  --query 'Parameter.Value' --output text)

export EC2_INSTANCE_ID=$(aws ec2 run-instances \
  --region "$AWS_REGION" \
  --image-id "$AMI_ID" \
  --instance-type "$EC2_INSTANCE_TYPE" \
  --key-name "$KEY_PAIR_NAME" \
  --security-group-ids "$WEB_SG_ID" \
  --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=${NAME_PREFIX}-web-01}]" \
  --query 'Instances[0].InstanceId' --output text)

aws ec2 wait instance-running --region "$AWS_REGION" --instance-ids "$EC2_INSTANCE_ID"

export ALLOC_ID=$(aws ec2 allocate-address --region "$AWS_REGION" --domain vpc --query 'AllocationId' --output text)
aws ec2 associate-address --region "$AWS_REGION" --instance-id "$EC2_INSTANCE_ID" --allocation-id "$ALLOC_ID"

export EC2_PUBLIC_IP=$(aws ec2 describe-instances --region "$AWS_REGION" --instance-ids "$EC2_INSTANCE_ID" --query 'Reservations[0].Instances[0].PublicIpAddress' --output text)
```

## 3) RDS PostgreSQL (lean)

```bash
export DB_SUBNET_GROUP="${NAME_PREFIX}-db-subnet-group"

# Use default subnets for first launch
export DEFAULT_SUBNET_IDS=$(aws ec2 describe-subnets --region "$AWS_REGION" --filters Name=default-for-az,Values=true --query 'Subnets[].SubnetId' --output text)

aws rds create-db-subnet-group \
  --region "$AWS_REGION" \
  --db-subnet-group-name "$DB_SUBNET_GROUP" \
  --db-subnet-group-description "${NAME_PREFIX} db subnet group" \
  --subnet-ids $DEFAULT_SUBNET_IDS

export DB_INSTANCE_ID="${NAME_PREFIX}-db-01"

aws rds create-db-instance \
  --region "$AWS_REGION" \
  --db-instance-identifier "$DB_INSTANCE_ID" \
  --engine postgres \
  --engine-version "16.4" \
  --db-instance-class "$DB_INSTANCE_CLASS" \
  --allocated-storage 20 \
  --storage-type gp3 \
  --master-username "$DB_USER" \
  --master-user-password "$DB_PASSWORD" \
  --db-name "$DB_NAME" \
  --vpc-security-group-ids "$DB_SG_ID" \
  --db-subnet-group-name "$DB_SUBNET_GROUP" \
  --backup-retention-period 7 \
  --no-publicly-accessible \
  --no-multi-az

aws rds wait db-instance-available --region "$AWS_REGION" --db-instance-identifier "$DB_INSTANCE_ID"

export DB_ENDPOINT=$(aws rds describe-db-instances \
  --region "$AWS_REGION" \
  --db-instance-identifier "$DB_INSTANCE_ID" \
  --query 'DBInstances[0].Endpoint.Address' --output text)
```

## 3b) Attach EC2 IAM role (SSM + S3 access)

```bash
export IAM_ROLE_NAME="${NAME_PREFIX}-ec2-role"
export IAM_PROFILE_NAME="${NAME_PREFIX}-ec2-profile"
export IAM_POLICY_NAME="${NAME_PREFIX}-ec2-policy"

cat > /tmp/trust-policy.json <<'EOF'
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Service": "ec2.amazonaws.com" },
      "Action": "sts:AssumeRole"
    }
  ]
}
EOF

cat > /tmp/permissions-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ssm:GetParameter",
        "ssm:GetParameters",
        "ssm:GetParametersByPath"
      ],
      "Resource": "arn:aws:ssm:${AWS_REGION}:${ACCOUNT_ID}:parameter${PARAM_PATH}*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket"
      ],
      "Resource": "arn:aws:s3:::${S3_ASSETS_BUCKET}"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject"
      ],
      "Resource": "arn:aws:s3:::${S3_ASSETS_BUCKET}/*"
    }
  ]
}
EOF

aws iam create-role \
  --role-name "$IAM_ROLE_NAME" \
  --assume-role-policy-document file:///tmp/trust-policy.json

aws iam put-role-policy \
  --role-name "$IAM_ROLE_NAME" \
  --policy-name "$IAM_POLICY_NAME" \
  --policy-document file:///tmp/permissions-policy.json

aws iam create-instance-profile --instance-profile-name "$IAM_PROFILE_NAME"
aws iam add-role-to-instance-profile --instance-profile-name "$IAM_PROFILE_NAME" --role-name "$IAM_ROLE_NAME"

# IAM propagation delay
sleep 10

aws ec2 associate-iam-instance-profile \
  --region "$AWS_REGION" \
  --instance-id "$EC2_INSTANCE_ID" \
  --iam-instance-profile Name="$IAM_PROFILE_NAME"
```

## 4) S3 asset bucket

```bash
aws s3api create-bucket \
  --region "$AWS_REGION" \
  --bucket "$S3_ASSETS_BUCKET"

aws s3api put-bucket-versioning \
  --region "$AWS_REGION" \
  --bucket "$S3_ASSETS_BUCKET" \
  --versioning-configuration Status=Enabled
```

## 5) SSM parameters (app secrets/config)

```bash
export DB_CONN="Host=${DB_ENDPOINT};Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true"

aws ssm put-parameter --region "$AWS_REGION" --name "${PARAM_PATH}/ConnectionStrings__DefaultConnection" --type SecureString --value "$DB_CONN" --overwrite
aws ssm put-parameter --region "$AWS_REGION" --name "${PARAM_PATH}/Stripe__SecretKey"              --type SecureString --value "<stripe-secret>" --overwrite
aws ssm put-parameter --region "$AWS_REGION" --name "${PARAM_PATH}/Stripe__WebhookSecret"          --type SecureString --value "<stripe-webhook-secret>" --overwrite
aws ssm put-parameter --region "$AWS_REGION" --name "${PARAM_PATH}/Stripe__PublishableKey"         --type String       --value "<stripe-publishable-key>" --overwrite
aws ssm put-parameter --region "$AWS_REGION" --name "${PARAM_PATH}/FileStorage__Provider"          --type String       --value "S3" --overwrite
aws ssm put-parameter --region "$AWS_REGION" --name "${PARAM_PATH}/FileStorage__S3__BucketName"    --type String       --value "$S3_ASSETS_BUCKET" --overwrite
aws ssm put-parameter --region "$AWS_REGION" --name "${PARAM_PATH}/FileStorage__S3__Region"        --type String       --value "$AWS_REGION" --overwrite
```

## 6) EC2 bootstrap

```bash
ssh -i "${KEY_PAIR_NAME}.pem" ubuntu@"${EC2_PUBLIC_IP}"
```

Run on EC2:

```bash
sudo apt-get update
sudo apt-get install -y nginx certbot python3-certbot-nginx unzip jq awscli

# Install .NET 9 runtime
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-9.0

sudo useradd --system --create-home --shell /usr/sbin/nologin stitchlens || true
sudo mkdir -p /opt/stitchlens/releases /opt/stitchlens/shared
sudo chown -R stitchlens:stitchlens /opt/stitchlens
```

Copy templates from repo:
- `StitchLens.Web/deploy/aws/stitchlens.service`
- `StitchLens.Web/deploy/aws/nginx.stitchlens.conf`
- `StitchLens.Web/deploy/aws/deploy.sh`
- `StitchLens.Web/deploy/aws/load-env-from-ssm.sh`
- `StitchLens.Web/deploy/aws/verify-env.sh`

Local machine copy commands:

```bash
scp -i "${KEY_PAIR_NAME}.pem" StitchLens.Web/deploy/aws/stitchlens.service ubuntu@"${EC2_PUBLIC_IP}":/tmp/
scp -i "${KEY_PAIR_NAME}.pem" StitchLens.Web/deploy/aws/nginx.stitchlens.conf ubuntu@"${EC2_PUBLIC_IP}":/tmp/
scp -i "${KEY_PAIR_NAME}.pem" StitchLens.Web/deploy/aws/deploy.sh ubuntu@"${EC2_PUBLIC_IP}":/tmp/
scp -i "${KEY_PAIR_NAME}.pem" StitchLens.Web/deploy/aws/load-env-from-ssm.sh ubuntu@"${EC2_PUBLIC_IP}":/tmp/
scp -i "${KEY_PAIR_NAME}.pem" StitchLens.Web/deploy/aws/verify-env.sh ubuntu@"${EC2_PUBLIC_IP}":/tmp/
```

On EC2:

```bash
sudo mkdir -p /etc/stitchlens
sudo mv /tmp/stitchlens.service /etc/systemd/system/stitchlens.service
sudo mv /tmp/nginx.stitchlens.conf /etc/nginx/sites-available/stitchlens
sudo ln -sf /etc/nginx/sites-available/stitchlens /etc/nginx/sites-enabled/stitchlens
sudo mv /tmp/load-env-from-ssm.sh /usr/local/bin/stitchlens-load-env
sudo mv /tmp/verify-env.sh /usr/local/bin/stitchlens-verify-env
sudo chmod +x /usr/local/bin/stitchlens-load-env
sudo chmod +x /usr/local/bin/stitchlens-verify-env

export AWS_REGION="us-east-1"
export PARAM_PATH="/stitchlens/prod"
sudo /usr/local/bin/stitchlens-load-env "${AWS_REGION}" "${PARAM_PATH}"
sudo /usr/local/bin/stitchlens-verify-env

sudo nginx -t
sudo systemctl daemon-reload
sudo systemctl enable stitchlens
```

To refresh env from SSM after secret updates:

```bash
sudo /usr/local/bin/stitchlens-load-env "${AWS_REGION}" "${PARAM_PATH}"
sudo /usr/local/bin/stitchlens-verify-env
sudo systemctl restart stitchlens
```

## 7) Deploy skeleton

Local machine (from repo root):

```bash
dotnet build StitchLens.sln
dotnet test StitchLens.sln
dotnet publish StitchLens.Web -c Release -o ./publish

tar -czf stitchlens-release.tgz -C ./publish .
scp -i "${KEY_PAIR_NAME}.pem" stitchlens-release.tgz ubuntu@"${EC2_PUBLIC_IP}":/tmp/
```

On EC2:

```bash
sudo mv /tmp/deploy.sh /usr/local/bin/stitchlens-deploy
sudo chmod +x /usr/local/bin/stitchlens-deploy
sudo /usr/local/bin/stitchlens-deploy /tmp/stitchlens-release.tgz
```

## 8) Nginx and TLS

On EC2:

```bash
sudo cp /path/to/nginx.stitchlens.conf /etc/nginx/sites-available/stitchlens
sudo ln -sf /etc/nginx/sites-available/stitchlens /etc/nginx/sites-enabled/stitchlens
sudo nginx -t
sudo systemctl reload nginx

sudo certbot --nginx -d <your-fqdn>
```

## 9) Smoke checks

```bash
curl -i https://<your-fqdn>/health/live
curl -i https://<your-fqdn>/health/ready
```

Functional checks:
- login flow
- upload -> configure -> preview
- PDF generation and download
- Stripe webhook endpoint reachability

## 10) Rollback

On EC2:

```bash
ls -1 /opt/stitchlens/releases
# choose previous release folder and relink
sudo ln -sfn /opt/stitchlens/releases/<previous-release-folder> /opt/stitchlens/current
sudo systemctl restart stitchlens
curl -i http://127.0.0.1:5000/health/live
```

## Notes
- Keep DB private (`--no-publicly-accessible`).
- Keep ingress `22` restricted to your IP.
- Add CloudWatch alarms after first successful deployment.
- Configure Route 53 DNS before certbot step.
