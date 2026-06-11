from PIL import Image
import numpy as np
import os

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

# Extract red coordinates
red_pts = []
for y in range(container_h):
    for x in range(container_w):
        if is_red_orange(*pixels[x, y]):
            red_pts.append((y, x))
red_pts = np.array(red_pts)

def evaluate_coords_fast(pts):
    sampled_pts = []
    for i in range(len(pts)):
        p1 = pts[i]
        p2 = pts[(i + 1) % len(pts)]
        # Sample 10 points along each line segment of the polygon
        for t in np.linspace(0, 1, 10):
            x = p1[0] + t * (p2[0] - p1[0])
            y = p1[1] + t * (p2[1] - p1[1])
            sampled_pts.append((y, x))
            
    sampled_pts = np.array(sampled_pts)
    
    # Calculate distance to nearest red pixel
    total_dist = 0.0
    for pt in sampled_pts:
        dists_sq = np.sum((red_pts - pt)**2, axis=1)
        total_dist += np.sqrt(np.min(dists_sq))
    return total_dist / len(sampled_pts)

# Initial guess based on shift dx=60, dy=30
current_pts = [(75, 180), (210, 45), (375, 45), (340, 280), (310, 280), (280, 395)]

# Fast local coordinate descent
improved = True
iteration = 0
while improved and iteration < 5:
    improved = False
    iteration += 1
    for i in range(len(current_pts)):
        best_pt = current_pts[i]
        best_loss = evaluate_coords_fast(current_pts)
        for dx in range(-6, 7, 2):
            for dy in range(-6, 7, 2):
                if dx == 0 and dy == 0:
                    continue
                test_pts = list(current_pts)
                test_pts[i] = (current_pts[i][0] + dx, current_pts[i][1] + dy)
                loss = evaluate_coords_fast(test_pts)
                if loss < best_loss:
                    best_loss = loss
                    best_pt = test_pts[i]
                    improved = True
        current_pts[i] = best_pt

# 1-pixel step fine search
for i in range(len(current_pts)):
    best_pt = current_pts[i]
    best_loss = evaluate_coords_fast(current_pts)
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            test_pts = list(current_pts)
            test_pts[i] = (current_pts[i][0] + dx, current_pts[i][1] + dy)
            loss = evaluate_coords_fast(test_pts)
            if loss < best_loss:
                best_loss = loss
                best_pt = test_pts[i]
    current_pts[i] = best_pt

points_str = " ".join([f"{x},{y}" for x, y in current_pts])
print("OPTIMIZED_POINTS:", points_str)
