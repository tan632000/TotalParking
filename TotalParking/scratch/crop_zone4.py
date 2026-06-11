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

# Crop Zone 4 region
rx = (250, 650)
ry = (0, 550)

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

# Also draw Zone 3 optimized polygon for context
pts3 = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
pts3_local = [(x - rx[0], y - ry[0]) for x, y in pts3]
draw.polygon(pts3_local, outline=(255, 165, 0), width=2)

os.makedirs('TotalParking/scratch', exist_ok=True)
crop.save('TotalParking/scratch/zone4_grid_crop.png')
print("Successfully generated zone4_grid_crop.png")
