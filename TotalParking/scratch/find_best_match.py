from PIL import Image, ImageDraw
import numpy as np

img_path = 'TotalParking/Images/zones_map.jpeg'
img = Image.open(img_path)

container_w = 1016
container_h = 781

disp_w = container_w
disp_h = int(img.height * (container_w / img.width))
pad_y = int((container_h - disp_h) / 2.0)

img_resized = img.resize((disp_w, disp_h), Image.Resampling.LANCZOS)
canvas = Image.new('RGB', (container_w, container_h), (15, 23, 42))
canvas.paste(img_resized, (0, pad_y))

pixels = canvas.load()

def is_red_orange(r, g, b):
    return (r > 130 and g < 90 and b < 90) or (r > 150 and 50 < g < 130 and b < 80)

# Build a binary grid of red pixels
red_grid = np.zeros((container_h, container_w), dtype=bool)
for y in range(container_h):
    for x in range(container_w):
        if is_red_orange(*pixels[x, y]):
            red_grid[y, x] = True

# Get the list of red coordinates as a Nx2 array
red_pts = np.array(np.where(red_grid)).T  # shape is (N, 2), values are (y, x)

def compute_average_distance(poly_pts):
    # Rasterize the polygon boundary
    mask = Image.new('L', (container_w, container_h), 0)
    draw = ImageDraw.Draw(mask)
    draw.polygon(poly_pts, outline=255, fill=0)
    
    # Get all outline pixel coordinates
    outline_y, outline_x = np.where(np.array(mask) == 255)
    if len(outline_y) == 0:
        return float('inf')
    
    outline_pts = np.stack([outline_y, outline_x], axis=1) # (M, 2)
    
    # For each outline point, find the distance to the nearest red point
    # To speed it up, we can use a KDTree if scipy is available, or just a simple matrix operation
    # Since M is around 1000 and N is around 5000, a direct broadcast might be slow or take too much memory.
    # Let's do a fast distance computation
    total_dist = 0.0
    for pt in outline_pts:
        # Euclidean distance squared to all red points
        dists_sq = np.sum((red_pts - pt)**2, axis=1)
        min_dist = np.sqrt(np.min(dists_sq))
        total_dist += min_dist
        
    return total_dist / len(outline_pts)

# Test candidates
candidates = {
    "Old Polygon": [(15, 150), (150, 15), (315, 15), (280, 250), (250, 250), (220, 365)],
    "Candidate 1 (Shift dx=65, dy=30)": [(15+65, 150+30), (150+65, 15+30), (315+65, 15+30), (280+65, 250+30), (250+65, 250+30), (220+65, 365+30)],
    "Candidate 2 (Tuned intersection)": [(74, 173), (205, 45), (380, 45), (348, 280), (318, 280), (285, 395)],
    "Candidate 3 (Tuned Shift dx=60, dy=30)": [(15+60, 150+30), (150+60, 15+30), (315+60, 15+30), (280+60, 250+30), (250+60, 250+30), (220+60, 365+30)],
    "Candidate 4 (Tuned Shift dx=60, dy=35)": [(15+60, 150+35), (150+60, 15+35), (315+60, 15+35), (280+60, 250+35), (250+60, 250+35), (220+60, 365+35)]
}

# Run evaluation
for name, pts in candidates.items():
    avg_d = compute_average_distance(pts)
    print(f"{name}: Average distance to red border = {avg_d:.3f} pixels")
