#!/usr/bin/env python3
"""
Color Card Extractor for StitchLens
Extract accurate yarn colors from photos of physical color cards.

Usage:
    python3 extract_colors_from_card.py <image_path> [--output colors.json]

Requirements:
    pip install opencv-python numpy pillow scikit-image --break-system-packages
"""

import cv2
import numpy as np
import json
import argparse
from pathlib import Path
import sys

def rgb_to_hex(r, g, b):
    """Convert RGB to hex color"""
    return f"#{int(r):02X}{int(g):02X}{int(b):02X}"

def rgb_to_lab(r, g, b):
    """
    Convert RGB to LAB color space using OpenCV
    Returns L, a, b values
    """
    # Create a 1x1 image with the RGB values
    rgb = np.uint8([[[b, g, r]]])  # OpenCV uses BGR
    lab = cv2.cvtColor(rgb, cv2.COLOR_BGR2LAB)
    L, a, b_val = lab[0][0]
    
    # Convert to proper LAB range
    L = float(L) * 100.0 / 255.0
    a = float(a) - 128.0
    b_val = float(b_val) - 128.0
    
    return round(L, 1), round(a, 1), round(b_val, 1)

def extract_grid_colors(image_path, grid_rows=None, grid_cols=None):
    """
    Extract colors from a grid-style color card.
    
    Args:
        image_path: Path to the color card image
        grid_rows: Number of rows in the grid (None = auto-detect)
        grid_cols: Number of columns in the grid (None = auto-detect)
    
    Returns:
        List of (x, y, r, g, b) tuples for each color sample
    """
    # Load image
    img = cv2.imread(str(image_path))
    if img is None:
        raise ValueError(f"Could not load image: {image_path}")
    
    height, width = img.shape[:2]
    print(f"Image size: {width}x{height}")
    
    # Convert to RGB
    img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    
    # If grid dimensions not specified, use defaults
    if grid_rows is None:
        grid_rows = 10  # Default assumption
    if grid_cols is None:
        grid_cols = 10  # Default assumption
    
    print(f"Using grid: {grid_rows} rows x {grid_cols} cols")
    
    # Calculate cell dimensions
    cell_height = height // grid_rows
    cell_width = width // grid_cols
    
    # Extract color from center of each cell
    colors = []
    for row in range(grid_rows):
        for col in range(grid_cols):
            # Calculate center of cell
            center_x = col * cell_width + cell_width // 2
            center_y = row * cell_height + cell_height // 2
            
            # Sample a small area around center to average out noise
            sample_size = min(cell_width, cell_height) // 4
            y1 = max(0, center_y - sample_size)
            y2 = min(height, center_y + sample_size)
            x1 = max(0, center_x - sample_size)
            x2 = min(width, center_x + sample_size)
            
            # Get average color in sample area
            sample = img_rgb[y1:y2, x1:x2]
            avg_color = np.mean(sample, axis=(0, 1))
            r, g, b = avg_color
            
            colors.append((row, col, r, g, b))
    
    return colors

def manual_color_picker(image_path):
    """
    Interactive tool to manually click on color samples.
    Click on each yarn sample, press 's' to save, 'q' to quit.
    """
    img = cv2.imread(str(image_path))
    if img is None:
        raise ValueError(f"Could not load image: {image_path}")
    
    colors = []
    current_point = None
    
    def mouse_callback(event, x, y, flags, param):
        nonlocal current_point
        if event == cv2.EVENT_LBUTTONDOWN:
            # Sample a small area around click
            sample_size = 10
            y1 = max(0, y - sample_size)
            y2 = min(img.shape[0], y + sample_size)
            x1 = max(0, x - sample_size)
            x2 = min(img.shape[1], x + sample_size)
            
            sample = img[y1:y2, x1:x2]
            avg_color = np.mean(sample, axis=(0, 1))
            b, g, r = avg_color
            
            current_point = (x, y, r, g, b)
            colors.append(current_point)
            
            # Draw a circle at the clicked point
            cv2.circle(img, (x, y), 5, (0, 255, 0), -1)
            print(f"✓ Sampled color {len(colors)}: RGB({int(r)}, {int(g)}, {int(b)})")
    
    # Create window and set mouse callback
    window_name = "Color Card - Click on each sample, 's' to save, 'q' to quit"
    cv2.namedWindow(window_name)
    cv2.setMouseCallback(window_name, mouse_callback)
    
    # Display instructions
    print("\n" + "="*60)
    print("MANUAL COLOR PICKER")
    print("="*60)
    print("Instructions:")
    print("  1. Click on the center of each yarn sample")
    print("  2. Press 's' to save all sampled colors")
    print("  3. Press 'q' to quit without saving")
    print("  4. Press 'r' to reset and start over")
    print("="*60 + "\n")
    
    while True:
        cv2.imshow(window_name, img)
        key = cv2.waitKey(1) & 0xFF
        
        if key == ord('q'):
            cv2.destroyAllWindows()
            return None
        elif key == ord('s'):
            cv2.destroyAllWindows()
            return colors
        elif key == ord('r'):
            # Reload image and reset colors
            img = cv2.imread(str(image_path))
            colors = []
            print("\n✓ Reset - start over\n")

def create_color_database(colors, start_code=None, codes=None):
    """
    Create a JSON database from extracted colors.
    
    Args:
        colors: List of (row, col, r, g, b) or (x, y, r, g, b) tuples
        start_code: Starting code number (e.g., 100)
        codes: List of specific color codes (overrides start_code)
    
    Returns:
        List of color dictionaries
    """
    color_db = []
    
    for i, color_data in enumerate(colors):
        if len(color_data) == 5:
            _, _, r, g, b = color_data
        else:
            r, g, b = color_data[:3]
        
        # Determine color code
        if codes and i < len(codes):
            code = codes[i]
        elif start_code:
            code = str(start_code + i)
        else:
            code = str(i)
        
        # Convert to hex and LAB
        hex_color = rgb_to_hex(r, g, b)
        L, a, b_val = rgb_to_lab(r, g, b)
        
        color_entry = {
            "code": code,
            "name": f"Color {code}",  # User should update with actual names
            "hex": hex_color,
            "lab_l": L,
            "lab_a": a,
            "lab_b": b_val
        }
        
        color_db.append(color_entry)
    
    return color_db

def main():
    parser = argparse.ArgumentParser(
        description="Extract colors from physical yarn color card photos"
    )
    parser.add_argument("image", help="Path to color card image")
    parser.add_argument("--output", "-o", default="extracted_colors.json",
                        help="Output JSON file (default: extracted_colors.json)")
    parser.add_argument("--mode", "-m", choices=["grid", "manual"], default="manual",
                        help="Extraction mode: grid (auto) or manual (click)")
    parser.add_argument("--rows", type=int, help="Number of grid rows (grid mode)")
    parser.add_argument("--cols", type=int, help="Number of grid columns (grid mode)")
    parser.add_argument("--start-code", type=int, default=100,
                        help="Starting color code number (default: 100)")
    parser.add_argument("--codes", help="Comma-separated list of color codes")
    
    args = parser.parse_args()
    
    image_path = Path(args.image)
    if not image_path.exists():
        print(f"❌ Error: Image file not found: {image_path}")
        return 1
    
    print(f"\n📷 Loading image: {image_path}")
    
    try:
        # Extract colors based on mode
        if args.mode == "grid":
            colors = extract_grid_colors(image_path, args.rows, args.cols)
            print(f"✓ Extracted {len(colors)} colors from grid")
        else:
            colors = manual_color_picker(str(image_path))
            if colors is None:
                print("❌ Cancelled")
                return 1
            print(f"\n✓ Sampled {len(colors)} colors manually")
        
        # Parse color codes if provided
        codes = None
        if args.codes:
            codes = [c.strip() for c in args.codes.split(',')]
            if len(codes) != len(colors):
                print(f"⚠️  Warning: {len(codes)} codes provided but {len(colors)} colors extracted")
        
        # Create database
        color_db = create_color_database(colors, args.start_code, codes)
        
        # Save to file
        output_path = Path(args.output)
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(color_db, f, indent='\t', ensure_ascii=False)
        
        print(f"\n✅ Saved {len(color_db)} colors to: {output_path}")
        print("\n⚠️  Important: Update the 'name' fields with actual color names!")
        print("   Edit the JSON file to add proper names from your color card.")
        
        return 0
        
    except Exception as e:
        print(f"❌ Error: {e}")
        import traceback
        traceback.print_exc()
        return 1

if __name__ == "__main__":
    sys.exit(main())