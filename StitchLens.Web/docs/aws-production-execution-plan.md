# AWS Production Execution Plan (Lean Option B)

This plan defines how to launch StitchLens on AWS with low monthly burn while keeping a clean path to scale.

## Locked decisions
- Region: `us-east-1`
- TLS: `Nginx + Let's Encrypt`
- Runtime architecture: x86

## Target architecture
- App host: EC2 `t3.small` (Ubuntu 24.04 LTS)
- Database: RDS PostgreSQL `db.t3.micro`, Single-AZ, `gp3` (20-30 GB)
- File storage: S3 for uploads and generated PDFs
- Reverse proxy: Nginx
- Certificates: certbot (Let's Encrypt)
- Secrets: SSM Parameter Store (`SecureString`)
- Monitoring: CloudWatch (logs and basic alarms)

## Phase 1 - AWS provisioning

1. Create core resources:
   - VPC/subnets/route tables (or default VPC for first launch)
   - EC2 instance: `stitchlens-prod-web-01`
   - RDS instance: `stitchlens-prod-db-01`
   - S3 bucket: `stitchlens-prod-assets-<accountid>`
   - Elastic IP for EC2
2. Configure security groups:
   - `stitchlens-prod-web-sg` inbound: `22` (trusted IP), `80`, `443`
   - `stitchlens-prod-db-sg` inbound: `5432` from `stitchlens-prod-web-sg` only
3. Configure RDS:
   - PostgreSQL 16/17
   - public access disabled
   - automated backups (7 days)
4. DNS:
   - Route 53 `A` record to EC2 Elastic IP (for example `app.stitchlens.com`)

## Phase 2 - App readiness changes

1. Database provider switch:
   - SQLite for local/dev
   - PostgreSQL (Npgsql) for production
2. Storage abstraction:
   - Introduce `IFileStorageService`
   - local-disk implementation for dev
   - S3 implementation for prod
3. Refactor upload/PDF reads and writes in controllers/services to use storage service.
4. Keep production configuration validation strict:
   - `ConnectionStrings:DefaultConnection`
   - `Stripe:SecretKey`
   - `Stripe:WebhookSecret`
   - S3 bucket and region settings
5. Validate EF migration flow for PostgreSQL.

## Phase 3 - EC2 runtime setup

1. Install packages:
   - .NET 9 runtime
   - nginx
   - certbot and `python3-certbot-nginx`
2. Create directories:
   - `/opt/stitchlens/releases`
   - `/opt/stitchlens/current`
   - `/opt/stitchlens/shared`
3. Configure `systemd` service (`stitchlens.service`) to run app on `127.0.0.1:5000`.
4. Configure Nginx as reverse proxy to `127.0.0.1:5000`.
5. Issue certificate:
   - `certbot --nginx -d <your-domain>`

## Phase 4 - Secrets and configuration

Store and load these as environment variables (from SSM):

- `ConnectionStrings__DefaultConnection`
- `Stripe__SecretKey`
- `Stripe__WebhookSecret`
- `Stripe__PublishableKey`
- `FileStorage__Provider=S3`
- `FileStorage__S3__BucketName`
- `FileStorage__S3__Region=us-east-1`
- Email SMTP settings
- Social auth keys (if enabled)

IAM role for EC2 should include minimum required access:
- S3 read/write for asset bucket
- SSM parameter read
- KMS decrypt (if SSM secure strings use CMK)

## Phase 5 - Deployment process

### Build/publish
- `dotnet build StitchLens.sln`
- `dotnet test StitchLens.sln`
- `dotnet publish StitchLens.Web -c Release`

### Deploy
1. Copy artifact to a new release folder on EC2.
2. Run migrations against RDS.
3. Switch `current` symlink to new release.
4. Restart `stitchlens.service`.
5. Smoke check:
   - `/health/live`
   - `/health/ready`
   - login/upload/preview/PDF download

### Rollback
1. Point `current` symlink to prior release.
2. Restart `stitchlens.service`.
3. Verify health endpoints.

## Phase 6 - Launch hardening validation

Execute and capture evidence for:
- Stripe webhook signature validation (valid and invalid signatures)
- migration execution proof
- RDS backup and restore drill
- rollback drill

Update:
- `StitchLens.Web/docs/Launch_Hardening_Checklist.md`
- `StitchLens.Web/docs/launch-hardening-evidence/go-no-go/decision.md`

## Phase 7 - Monitoring and cost controls

1. CloudWatch alarms:
   - EC2 CPU/disk/service-down signals
   - app 5xx spikes
   - RDS CPU/connections/storage
2. Cost controls:
   - CloudWatch log retention (14-30 days)
   - S3 lifecycle for stale generated assets
   - monthly right-size review for EC2 and RDS

## Expected monthly envelope
- Typical: `$60-$100`
- Most likely range for early traffic: `$70-$90`

## Open choices to finalize
- Production FQDN (exact domain)
- Deployment style (manual SSH vs CI/CD pipeline)
- S3 retention policy for generated PDFs
