import os
from PIL import Image, ImageDraw, ImageFont

# Load the new image and resize/pad it to 1016x781
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

# Let's define the 6 crop regions around the 6 vertices of Zone 3
# We will draw a local grid with labels.
vertices_info = [
    {"name": "v1_leftmost", "center": (50, 150), "range_x": (0, 150), "range_y": (80, 220)},
    {"name": "v2_top_left", "center": (150, 15), "range_x": (100, 250), "range_y": (0, 100)},
    {"name": "v3_top_right", "center": (315, 15), "range_x": (250, 400), "range_y": (0, 120)},
    {"name": "v4_right_bend1", "center": (280, 250), "range_x": (220, 360), "range_y": (200, 320)},
    {"name": "v5_right_bend2", "center": (250, 250), "range_x": (200, 320), "range_y": (200, 320)},
    {"name": "v6_bottom_right", "center": (220, 365), "range_x": (160, 280), "range_y": (320, 440)},
]

os.makedirs('TotalParking/scratch/crops', exist_ok=True)
font = ImageFont.load_default()

for v in vertices_info:
    rx = v["range_x"]
    ry = v["range_y"]
    
    # Crop the canvas
    crop_w = rx[1] - rx[0]
    crop_h = ry[1] - ry[0]
    crop = canvas.crop((rx[0], ry[0], rx[1], ry[1]))
    
    # Draw local grid on the crop
    draw = ImageDraw.Draw(crop)
    
    # Draw vertical lines
    for x in range(rx[0], rx[1], 10):
        lx = x - rx[0]
        color = (255, 0, 0) if x % 50 == 0 else (128, 128, 128)
        draw.line([(lx, 0), (lx, crop_h)], fill=color, width=1)
        if x % 20 == 0:
            draw.text((lx + 2, 5), str(x), fill=(255, 255, 0), font=font)
            
    # Draw horizontal lines
    for y in range(ry[0], ry[1], 10):
        ly = y - ry[0]
        color = (255, 0, 0) if y % 50 == 0 else (128, 128, 128)
        draw.line([(0, ly), (crop_w, ly)], fill=color, width=1)
        if y % 20 == 0:
            draw.text((5, ly + 2), str(y), fill=(255, 255, 0), font=font)
            
    crop.save(f'TotalParking/scratch/crops/{v["name"]}.png')
    print(f"Saved crop for {v['name']}")
