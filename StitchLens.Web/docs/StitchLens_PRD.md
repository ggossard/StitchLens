# StitchLens Product Requirements Document (PRD)

## Summary Table
| Category | Details |
|-----------|----------|
| **Project Name** | StitchLens |
| **Tagline** | From photo to pattern — stitch your story. |
| **Owner** | Privately operated by founder (Grady) |
| **Initial Target Market** | North American needlepoint hobbyists (expanding globally) |
| **Launch Goal (MVP)** | Within 6 months of development start |
| **Primary KPIs** | Conversion rate (free → paid), PDF downloads, avg. revenue per user (ARPU), customer satisfaction |

---

## 1. Overview & Objectives
**Purpose:**  
To provide a web-based platform that allows users to upload photos and instantly generate professional-quality needlepoint canvas templates, complete with stitch grids, yarn color mapping, and printable PDF charts.

**Objectives:**  
- Simplify the process of converting photos into needlepoint patterns.  
- Deliver high-quality, color-accurate charts compatible with real yarn brands.  
- Build a scalable SaaS-style product that transitions from digital downloads to physical fulfillment (printed canvases & kits).  

---

## 2. User Personas
**1. Hobbyist Stitcher (Primary Persona)**  
- Female, 45–70, enjoys needlepoint as a creative pastime.  
- Values ease of use, visual quality, and printable patterns.  
- Likely to purchase yarn kits or printed canvases.  

**2. Craft Influencer / Designer (Secondary Persona)**  
- Semi-professional creator who sells designs or tutorials.  
- Seeks tools to speed up design creation and share results online.  
- Potential marketplace contributor in Phase 3.  

**3. Tech-Savvy Maker (Tertiary Persona)**  
- Comfortable with web tools and image editing.  
- Enjoys experimenting with color and mesh settings.  
- Early adopter; likely to provide feedback and share results.  

---

## 3. Core Workflows
1. **Photo Upload & Crop:** User uploads an image, optionally crops and resizes to target dimensions.  
2. **Canvas Setup:** User selects mesh count (10–18), stitch type, finished size, and color limit.  
3. **Color Quantization:** System reduces photo to N colors in perceptual LAB space.  
4. **Palette Mapping:** Colors matched to yarn catalog (DMC, Appleton, etc.) using ΔE2000.  
5. **Grid Rendering:** Output a grid with color-coded stitches and optional symbols.  
6. **Legend Generation:** Create color legend showing code, name, #stitches, yards, and skeins.  
7. **Export:** Generate downloadable PDF with grid pages, legend, and thumbnail.  
8. **Account & History (Phase 2):** Save projects and export history under user profile.  
9. **Marketplace (Phase 3):** Users can share or sell completed patterns.  

---

## 4. MVP Feature Set
| Category | Description |
|-----------|-------------|
| **Image Input** | Upload JPG/PNG, crop, scale, background trim (optional). |
| **Canvas Settings** | Mesh count, finished size (inches/stitches), stitch type (tent/basketweave). |
| **Color Reduction** | K-means or median-cut quantization; user chooses max colors (10–60). |
| **Palette Mapping** | Match quantized colors to yarn brands using stored catalog (JSON/DB). |
| **Grid Renderer** | Generate stitch grid overlay and legend; print-ready at 1:1 scale. |
| **PDF Export** | Multi-page PDF with cover, grid, legend, and test square. |
| **UI/UX** | Simple, responsive interface for desktop and tablet. |
| **Payments** | Stripe integration for one-time pattern purchases. |
| **Freemium Tier** | Low-res preview with watermark. |

---

## 5. Feature Roadmap
| Phase | Timeline | Key Features |
|--------|-----------|---------------|
| **MVP (0–6 mo)** | Upload → Quantize → PDF Export; Freemium + Paid tiers. |
| **V2 (6–12 mo)** | Accounts, project saving, yarn inventory tracking, improved rendering. |
| **V3 (12–24 mo)** | Printed canvas and kit dropship integration, Pro subscription tier. |
| **V4 (24–36 mo)** | Community pattern sharing, designer marketplace, affiliate sales. |
| **V5 (36+ mo)** | Mobile app, multi-language support, AI background removal. |

---

## 6. User Stories & Acceptance Criteria
**Story 1:** As a hobbyist, I want to upload a photo and see it transformed into a stitch grid so I can visualize my design.  
- *Acceptance:* The system must display a quantized preview within 10 seconds for standard images (<10MB).  

**Story 2:** As a user, I want to select a yarn brand so that my pattern uses available colors.  
- *Acceptance:* User can choose brand from a dropdown; legend shows brand codes and names.  

**Story 3:** As a user, I want to download a high-quality PDF so I can print and stitch from it.  
- *Acceptance:* PDF export includes grid pages, legend, and 1-inch calibration square; downloadable within 15 seconds.  

**Story 4:** As a returning customer, I want my project history saved.  
- *Acceptance:* Phase 2 introduces user accounts; patterns retrievable by title/date.  

**Story 5:** As a designer, I want to share or sell patterns to other users.  
- *Acceptance:* Marketplace (Phase 3) enables listing, price setting, and royalty tracking.  

---

## 7. Wireframe Descriptions (Structure Only)
**Home Page**  
- Hero area: Upload button + tagline.  
- Steps summary: Upload, Adjust, Download.  
- Call-to-action: Try Free / Sign In.  

**Pattern Generator Page**  
- Left pane: Image preview, crop tools, mesh and size settings.  
- Right pane: Color quantization controls, brand selector, preview button.  
- Footer: Generate PDF / Download buttons.  

**Results Page**  
- Download link (PDF), color legend preview, estimated skeins summary.  
- Social share option (Facebook/Pinterest).  

**Account Page (Phase 2)**  
- Project list with thumbnails, creation date, and re-download links.  
- Subscription management area.  

**Marketplace (Phase 3)**  
- Search and browse community designs.  
- Pattern detail page with preview and purchase button.  

---

## 8. Data Model Overview
| Entity | Key Fields |
|---------|-------------|
| **User** | id, name, email, password_hash, plan_type |
| **Project** | id, user_id, title, image_meta, mesh, width_in, height_in, stitch_type, color_limit, brand_id, legend_json, grid_file, pdf_file |
| **Palette** | id, brand_name, yarn_code, color_name, srgb, lab, yards_per_skein |
| **Order** | id, user_id, project_id, order_type (digital/kit), status, partner_ref |

---

## 9. Metrics & KPIs
- **Conversion Rate:** % of users upgrading from free to paid downloads.  
- **Export Success Rate:** % of pattern generations completed successfully.  
- **Avg. Revenue/User:** Track monthly recurring revenue from subscriptions.  
- **Fulfillment Accuracy:** % of correctly fulfilled canvas/kit orders (Phase 3+).  
- **User Retention:** 30/90-day repeat visits.  

---

## 10. Risks & Mitigations
| Risk | Mitigation |
|------|-------------|
| Partner delays or quality issues | Vet multiple print/kit vendors; maintain SLAs. |
| Color mismatch between digital & printed output | Use standardized LAB profiles and partner calibration tests. |
| Low user adoption | Focused social media ads and influencer outreach. |
| High compute costs | Use scalable cloud functions for image processing. |
| Intellectual property (photo rights) | Require user confirmation on upload. |

---

## 11. Future Enhancements
- AI-powered smart cropping and background removal.  
- Auto-yarn inventory integration (import from user-owned stock).  
- Collaborative design boards (share WIP with friends).  
- Mobile app for instant photo capture + conversion.  
- API access for third-party craft retailers.

---

### End of Document
