import os
from PIL import Image, ImageDraw, ImageFont

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

# Crop Zone 6 region
rx = (450, 980)
ry = (150, 680)

crop = canvas.crop((rx[0], ry[0], rx[1], ry[1]))
draw = ImageDraw.Draw(crop)
font = ImageFont.load_default()

crop_w = rx[1] - rx[0]
crop_h = ry[1] - ry[0]

# Draw coordinate grid
for x in range(rx[0], rx[1], 10):
    lx = x - rx[0]
    color = (255, 0, 0) if x % 50 == 0 else (60, 60, 60)
    draw.line([(lx, 0), (lx, crop_h)], fill=color, width=1)
    if x % 50 == 0:
        draw.text((lx + 2, 5), str(x), fill=(255, 255, 0), font=font)

for y in range(ry[0], ry[1], 10):
    ly = y - ry[0]
    color = (255, 0, 0) if y % 50 == 0 else (60, 60, 60)
    draw.line([(0, ly), (crop_w, ly)], fill=color, width=1)
    if y % 50 == 0:
        draw.text((5, ly + 2), str(y), fill=(255, 255, 0), font=font)

# Draw Zone 5 optimized polygon for context
pts5 = [(526,45), (923,46), (923,235), (718,223), (632,198), (526,211)]
pts5_local = [(x - rx[0], y - ry[0]) for x, y in pts5]
draw.polygon(pts5_local, outline=(6, 182, 212), width=2)

os.makedirs('TotalParking/scratch', exist_ok=True)
crop.save('TotalParking/scratch/zone6_grid_crop.png')
print("Successfully generated zone6_grid_crop.png")
