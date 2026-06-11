from PIL import Image, ImageDraw, ImageFont
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
canvas = Image.new('RGB', (container_w, container_h), (0, 0, 0))
canvas.paste(img_resized, (0, pad_y))

pixels = canvas.load()

def is_red_orange(r, g, b):
    return (r > 130 and g < 90 and b < 90) or (r > 150 and 50 < g < 130 and b < 80)

# Create an overlay image
overlay = Image.new('RGBA', (container_w, container_h), (0, 0, 0, 255))
draw = ImageDraw.Draw(overlay)

# Draw red pixels
for y in range(container_h):
    for x in range(container_w):
        r, g, b = pixels[x, y]
        if is_red_orange(r, g, b):
            overlay.putpixel((x, y), (255, 0, 0, 255))
        else:
            # make background dark grey so we can still see the building outline slightly
            gray = int(0.299*r + 0.587*g + 0.114*b) // 3
            overlay.putpixel((x, y), (gray, gray, gray, 255))

# Draw grid and coordinates
draw_overlay = ImageDraw.Draw(overlay)
for x in range(0, container_w, 50):
    draw_overlay.line([(x, 0), (x, container_h)], fill=(0, 255, 255, 50), width=1)
    draw_overlay.text((x + 2, 5), str(x), fill=(0, 255, 255, 255))

for y in range(0, container_h, 50):
    draw_overlay.line([(0, y), (container_w, y)], fill=(0, 255, 255, 50), width=1)
    draw_overlay.text((5, y + 2), str(y), fill=(0, 255, 255, 255))

overlay.save('TotalParking/scratch/red_mask_grid.png')
print("Successfully generated red_mask_grid.png")
