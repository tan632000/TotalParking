from PIL import Image, ImageDraw
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

# Zone 4 coordinates list (10 vertices)
# Old: 315,15 520,15 520,470 350,470 350,480 280,480 280,365 220,365 250,250 280,250
# Index mapping:
# idx 0: 315,15 -> SHARED (fixed to 343, 45)
# idx 1: 520,15 -> Free
# idx 2: 520,470 -> Free
# idx 3: 350,470 -> Free
# idx 4: 350,480 -> Free
# idx 5: 280,480 -> Free
# idx 6: 280,365 -> Free
# idx 7: 220,365 -> SHARED (fixed to 308, 390)
# idx 8: 250,250 -> SHARED (fixed to 278, 293)
# idx 9: 280,250 -> SHARED (fixed to 308, 248)

fixed_indices = {0: (343, 45), 7: (308, 390), 8: (278, 293), 9: (308, 248)}
free_indices = [1, 2, 3, 4, 5, 6]

current_pts = [
    (343, 45),    # 0 (fixed)
    (520+60, 15+30),  # 1 (initial guess with shift dx=60, dy=30)
    (520+60, 470+30), # 2
    (350+60, 470+30), # 3
    (350+60, 480+30), # 4
    (280+60, 480+30), # 5
    (280+60, 365+30), # 6
    (308, 390),   # 7 (fixed)
    (278, 293),   # 8 (fixed)
    (308, 248)    # 9 (fixed)
]

def evaluate_zone4(pts):
    sampled_pts = []
    for i in range(len(pts)):
        p1 = pts[i]
        p2 = pts[(i + 1) % len(pts)]
        # Sample points along segment
        for t in np.linspace(0, 1, 10):
            x = p1[0] + t * (p2[0] - p1[0])
            y = p1[1] + t * (p2[1] - p1[1])
            sampled_pts.append((y, x))
    sampled_pts = np.array(sampled_pts)
    total_dist = 0.0
    for pt in sampled_pts:
        dists_sq = np.sum((red_pts - pt)**2, axis=1)
        total_dist += np.sqrt(np.min(dists_sq))
    return total_dist / len(sampled_pts)

# Coordinate Descent on free vertices
improved = True
iteration = 0
while improved and iteration < 5:
    improved = False
    iteration += 1
    for i in free_indices:
        best_pt = current_pts[i]
        best_loss = evaluate_zone4(current_pts)
        for dx in range(-6, 7, 2):
            for dy in range(-6, 7, 2):
                if dx == 0 and dy == 0:
                    continue
                test_pts = list(current_pts)
                test_pts[i] = (current_pts[i][0] + dx, current_pts[i][1] + dy)
                loss = evaluate_zone4(test_pts)
                if loss < best_loss:
                    best_loss = loss
                    best_pt = test_pts[i]
                    improved = True
        current_pts[i] = best_pt

# Fine search
for i in free_indices:
    best_pt = current_pts[i]
    best_loss = evaluate_zone4(current_pts)
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            test_pts = list(current_pts)
            test_pts[i] = (current_pts[i][0] + dx, current_pts[i][1] + dy)
            loss = evaluate_zone4(test_pts)
            if loss < best_loss:
                best_loss = loss
                best_pt = test_pts[i]
    current_pts[i] = best_pt

# Print optimized coordinates
pts_str = " ".join([f"{int(x)},{int(y)}" for x, y in current_pts])
print("OPTIMIZED_ZONE4:", pts_str)

# Draw overlay of Zone 3 and Zone 4 for verification
draw = ImageDraw.Draw(canvas)
overlay = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
draw_overlay = ImageDraw.Draw(overlay)

# Zone 3 points
pts3 = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
# Draw Zone 3 in orange
draw_overlay.polygon(pts3, fill=(249, 115, 22, 100), outline=(249, 115, 22, 255))
# Draw Zone 4 in purple (fill: rgba(168, 85, 247, 0.15), stroke: rgb(168, 85, 247))
draw_overlay.polygon(current_pts, fill=(168, 85, 247, 100), outline=(168, 85, 247, 255))

canvas_rgba = canvas.convert('RGBA')
final_img = Image.alpha_composite(canvas_rgba, overlay)
os.makedirs('TotalParking/scratch', exist_ok=True)
final_img.save('TotalParking/scratch/zone4_verify.png')
print("Successfully generated zone4_verify.png")
