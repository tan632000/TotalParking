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

# Zone 1 coordinates (6 vertices)
# Old: 565,575 590,575 950,540 950,730 670,770 565,780
# idx 0: 565,575 -> SHARED (fixed to 573, 410)
# idx 1: 590,575 -> SHARED (fixed to 602, 410)
# idx 2: 950,540 -> SHARED (fixed to 923, 410)
# idx 3: 950,730 -> Free (X locked to 923, Y should be around 569)
# idx 4: 670,770 -> Free (initial guess scaled: X around 650, Y around 750)
# idx 5: 565,780 -> Free (X locked to 573, Y should be around 760)

current_pts = [
    (573, 410),   # 0 (fixed)
    (602, 410),   # 1 (fixed)
    (923, 410),   # 2 (fixed)
    (923, 569),   # 3 (X locked to 923)
    (650, 750),   # 4
    (573, 760)    # 5 (X locked to 573)
]

free_indices = [3, 4, 5]

def evaluate_zone1(pts):
    sampled_pts = []
    for i in range(len(pts)):
        p1 = pts[i]
        p2 = pts[(i + 1) % len(pts)]
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
        best_loss = evaluate_zone1(current_pts)
        for dx in range(-6, 7, 2):
            for dy in range(-6, 7, 2):
                if dx == 0 and dy == 0:
                    continue
                # Lock X=923 for point 3, X=573 for point 5
                actual_dx = 0 if i in [3, 5] else dx
                test_pts = list(current_pts)
                test_pts[i] = (current_pts[i][0] + actual_dx, current_pts[i][1] + dy)
                loss = evaluate_zone1(test_pts)
                if loss < best_loss:
                    best_loss = loss
                    best_pt = test_pts[i]
                    improved = True
        current_pts[i] = best_pt

# Fine search
for i in free_indices:
    best_pt = current_pts[i]
    best_loss = evaluate_zone1(current_pts)
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            actual_dx = 0 if i in [3, 5] else dx
            test_pts = list(current_pts)
            test_pts[i] = (current_pts[i][0] + actual_dx, current_pts[i][1] + dy)
            loss = evaluate_zone1(test_pts)
            if loss < best_loss:
                best_loss = loss
                best_pt = test_pts[i]
    current_pts[i] = best_pt

# Print optimized coordinates
pts_str = " ".join([f"{int(x)},{int(y)}" for x, y in current_pts])
print("OPTIMIZED_ZONE1:", pts_str)

# Draw overlay of Zone 3, 4, 5, 6, 1 for verification
draw = ImageDraw.Draw(canvas)
overlay = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
draw_overlay = ImageDraw.Draw(overlay)

pts3 = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
pts4 = [(343, 45), (526, 45), (526, 390), (412, 390), (412, 390), (372, 390), (372, 390), (308, 390), (278, 293), (308, 248)]
pts5 = [(526,45), (923,46), (923,235), (718,223), (632,198), (526,211)]
pts6 = [(526,211), (632,198), (718,223), (923,235), (923,410), (602,410), (602,380), (573,380)]

draw_overlay.polygon(pts3, fill=(249, 115, 22, 100), outline=(249, 115, 22, 255))
draw_overlay.polygon(pts4, fill=(168, 85, 247, 100), outline=(168, 85, 247, 255))
draw_overlay.polygon(pts5, fill=(6, 182, 212, 100), outline=(6, 182, 212, 255))
draw_overlay.polygon(pts6, fill=(34, 197, 94, 100), outline=(34, 197, 94, 255))
# Draw Zone 1 in Yellow (fill: rgba(234, 179, 8, 0.15), stroke: rgb(234, 179, 8))
draw_overlay.polygon(current_pts, fill=(234, 179, 8, 100), outline=(234, 179, 8, 255))

canvas_rgba = canvas.convert('RGBA')
final_img = Image.alpha_composite(canvas_rgba, overlay)
os.makedirs('TotalParking/scratch', exist_ok=True)
final_img.save('TotalParking/scratch/zone1_verify.png')
print("Successfully generated zone1_verify.png")
