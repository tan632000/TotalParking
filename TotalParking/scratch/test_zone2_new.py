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

# Define the new candidate polygons for Zone 1 and Zone 2
# Zone 2 candidate:
pts2 = [
    (308, 390),
    (602, 390),
    (602, 510),
    (573, 510),
    (573, 560),
    (494, 560)
]

# Zone 1 candidate (8 vertices to form a perfect triangle at the bottom and step on the left):
# Let's trace it:
# 1. (602, 410) - top-left
# 2. (923, 410) - top-right
# 3. (923, 572) - bottom-right diagonal start
# 4. (688, 735) - bottom tip
# 5. (494, 560) - wait, does Zone 1 start from the same bottom-left diagonal?
# Let's check: the bottom-left diagonal wall goes from (308, 390) to (688, 735).
# Since Zone 2 ends at Y = 560 (X = 494), Zone 1's diagonal border starts at (494, 560) and goes to (688, 735).
# So:
# 5. (494, 560) - bottom-left diagonal start
# 6. (573, 560) - inner bend
# 7. (573, 510) - vertical line step
# 8. (602, 510) - horizontal step
pts1 = [
    (602, 410),
    (923, 410),
    (923, 572),
    (688, 735),
    (494, 560),
    (573, 560),
    (573, 510),
    (602, 510)
]

# Let's draw overlay of all 6 zones
draw = ImageDraw.Draw(canvas)
overlay = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
draw_overlay = ImageDraw.Draw(overlay)

pts3 = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
pts4 = [(343, 45), (526, 45), (526, 390), (412, 390), (372, 390), (308, 390), (278, 293), (308, 248)]
pts5 = [(526,45), (923,46), (923,235), (718,223), (632,198), (526,211)]
pts6 = [(526,211), (632,198), (718,223), (923,235), (923,410), (602,410), (602,380), (573,380)]

draw_overlay.polygon(pts3, fill=(249, 115, 22, 100), outline=(249, 115, 22, 255))
draw_overlay.polygon(pts4, fill=(168, 85, 247, 100), outline=(168, 85, 247, 255))
draw_overlay.polygon(pts5, fill=(6, 182, 212, 100), outline=(6, 182, 212, 255))
draw_overlay.polygon(pts6, fill=(34, 197, 94, 100), outline=(34, 197, 94, 255))

# Draw Zone 1 in Yellow
draw_overlay.polygon(pts1, fill=(234, 179, 8, 100), outline=(234, 179, 8, 255))
# Draw Zone 2 in Grey
draw_overlay.polygon(pts2, fill=(148, 163, 184, 100), outline=(203, 213, 225, 255))

canvas_rgba = canvas.convert('RGBA')
final_img = Image.alpha_composite(canvas_rgba, overlay)
os.makedirs('TotalParking/scratch', exist_ok=True)
final_img.save('TotalParking/scratch/zone1_zone2_new_verify.png')
print("Successfully generated zone1_zone2_new_verify.png")
