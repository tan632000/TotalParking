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
    # Pure red, orange and red-orange lines
    return (r > 130 and g < 90 and b < 90) or (r > 150 and 50 < g < 130 and b < 80)

# Extract red coordinates
red_pts = []
for y in range(container_h):
    for x in range(container_w):
        if is_red_orange(*pixels[x, y]):
            red_pts.append((y, x))  # (y, x) for easy math
red_pts = np.array(red_pts)

def get_polygon_boundary_pixels(pts):
    # Rasterize outline
    mask = Image.new('L', (container_w, container_h), 0)
    draw = ImageDraw.Draw(mask)
    draw.polygon(pts, outline=255, fill=0)
    y_idx, x_idx = np.where(np.array(mask) == 255)
    return np.stack([y_idx, x_idx], axis=1)

def evaluate_coords(pts):
    outline = get_polygon_boundary_pixels(pts)
    if len(outline) == 0:
        return float('inf')
    
    # Calculate average distance to nearest red pixel
    total_dist = 0.0
    for pt in outline:
        # direct distance squared
        dists_sq = np.sum((red_pts - pt)**2, axis=1)
        total_dist += np.sqrt(np.min(dists_sq))
    return total_dist / len(outline)

# Initial guess based on Shift dx=60, dy=30
# Old: 15,150 150,15 315,15 280,250 250,250 220,365
# Shifted by 60, 30: 75,180 210,45 375,45 340,280 310,280 280,395
# Let's perform a local coordinate optimization.
# Since 6 vertices in 2D is a 12-dimensional search space, grid search is too slow.
# We will optimize each vertex one by one (Coordinate Descent) for a few iterations.
current_pts = [(75, 180), (210, 45), (375, 45), (340, 280), (310, 280), (280, 395)]

print("Initial Loss:", evaluate_coords(current_pts))

improved = True
iteration = 0
while improved and iteration < 5:
    improved = False
    iteration += 1
    print(f"\n--- Iteration {iteration} ---")
    for i in range(len(current_pts)):
        best_pt = current_pts[i]
        best_loss = evaluate_coords(current_pts)
        
        # Search local neighborhood
        for dx in range(-8, 9, 2):
            for dy in range(-8, 9, 2):
                if dx == 0 and dy == 0:
                    continue
                test_pts = list(current_pts)
                test_pts[i] = (current_pts[i][0] + dx, current_pts[i][1] + dy)
                
                loss = evaluate_coords(test_pts)
                if loss < best_loss:
                    best_loss = loss
                    best_pt = test_pts[i]
                    improved = True
                    
        if current_pts[i] != best_pt:
            print(f"Vertex {i+1} moved from {current_pts[i]} to {best_pt} (loss: {best_loss:.4f})")
            current_pts[i] = best_pt

# Final finer search (1 pixel step)
for i in range(len(current_pts)):
    best_pt = current_pts[i]
    best_loss = evaluate_coords(current_pts)
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            test_pts = list(current_pts)
            test_pts[i] = (current_pts[i][0] + dx, current_pts[i][1] + dy)
            loss = evaluate_coords(test_pts)
            if loss < best_loss:
                best_loss = loss
                best_pt = test_pts[i]
    current_pts[i] = best_pt

print("\nOptimized Points:")
points_str = " ".join([f"{x},{y}" for x, y in current_pts])
print(f'<polygon points="{points_str}" class="zone-polygon zone-3" />')
print("Final Loss:", evaluate_coords(current_pts))
