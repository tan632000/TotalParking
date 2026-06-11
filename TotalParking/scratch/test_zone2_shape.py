from PIL import Image, ImageDraw
import os
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

# Extract red coordinates
red_pts = []
for y in range(container_h):
    for x in range(container_w):
        if is_red_orange(*pixels[x, y]):
            red_pts.append((y, x))
red_pts = np.array(red_pts)

# Zone 2 candidate coordinates (11 vertices)
pts2 = [
    (308, 390),
    (372, 390),
    (372, 390),
    (412, 390),
    (412, 390),
    (526, 390),
    (573, 380),
    (602, 380),
    (602, 410),
    (573, 410),
    (573, 633)
]

# Calculate loss
sampled_pts = []
for i in range(len(pts2)):
    p1 = pts2[i]
    p2 = pts2[(i + 1) % len(pts2)]
    for t in np.linspace(0, 1, 10):
        x = p1[0] + t * (p2[0] - p1[0])
        y = p1[1] + t * (p2[1] - p1[1])
        sampled_pts.append((y, x))
sampled_pts = np.array(sampled_pts)

total_dist = 0.0
for pt in sampled_pts:
    dists_sq = np.sum((red_pts - pt)**2, axis=1)
    total_dist += np.sqrt(np.min(dists_sq))
avg_dist = total_dist / len(sampled_pts)

print(f"Average distance to red borders for Zone 2: {avg_dist:.3f} pixels")

# Draw overlay of all 6 zones
draw = ImageDraw.Draw(canvas)
overlay = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
draw_overlay = ImageDraw.Draw(overlay)

pts3 = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
pts4 = [(343, 45), (526, 45), (526, 390), (412, 390), (412, 390), (372, 390), (372, 390), (308, 390), (278, 293), (308, 248)]
pts5 = [(526,45), (923,46), (923,235), (718,223), (632,198), (526,211)]
pts6 = [(526,211), (632,198), (718,223), (923,235), (923,410), (602,410), (602,380), (573,380)]
pts1 = [(573,410), (602,410), (923,410), (923,572), (682,731), (573,728)]

draw_overlay.polygon(pts3, fill=(249, 115, 22, 100), outline=(249, 115, 22, 255))
draw_overlay.polygon(pts4, fill=(168, 85, 247, 100), outline=(168, 85, 247, 255))
draw_overlay.polygon(pts5, fill=(6, 182, 212, 100), outline=(6, 182, 212, 255))
draw_overlay.polygon(pts6, fill=(34, 197, 94, 100), outline=(34, 197, 94, 255))
draw_overlay.polygon(pts1, fill=(234, 179, 8, 100), outline=(234, 179, 8, 255))
# Draw Zone 2 in Grey (fill: rgba(148, 163, 184, 0.18), stroke: rgb(203, 213, 225))
draw_overlay.polygon(pts2, fill=(148, 163, 184, 100), outline=(203, 213, 225, 255))

canvas_rgba = canvas.convert('RGBA')
final_img = Image.alpha_composite(canvas_rgba, overlay)
os.makedirs('TotalParking/scratch', exist_ok=True)
final_img.save('TotalParking/scratch/zone2_verify.png')
print("Successfully generated zone2_verify.png")
