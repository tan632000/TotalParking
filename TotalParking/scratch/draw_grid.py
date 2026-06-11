import os
from PIL import Image, ImageDraw, ImageFont

# Load the image
img_path = 'TotalParking/Images/zones_map.jpeg'
img = Image.open(img_path)

# HTML aspect ratio dimensions
container_w = 1016
container_h = 781

# Compute scale and offsets for object-contain
scale_w = container_w / img.width
scale_h = container_h / img.height

# Since img.width/img.height (4800/3584 = 1.339) > container_w/container_h (1.301),
# the image is fitted to the container width.
disp_w = container_w
disp_h = int(img.height * (container_w / img.width))

# Letterbox vertical padding
pad_y = (container_h - disp_h) / 2.0

# Resize image to fit width
img_resized = img.resize((disp_w, disp_h), Image.Resampling.LANCZOS)

# Create a new canvas with container size (background color is slate-900 like HTML)
canvas = Image.new('RGB', (container_w, container_h), (15, 23, 42))
canvas.paste(img_resized, (0, int(pad_y)))

# Draw grid and coordinates
draw = ImageDraw.Draw(canvas)

# Let's draw horizontal and vertical lines every 20 pixels
# and label every 100 pixels.
font = ImageFont.load_default()

for x in range(0, container_w, 20):
    color = (100, 100, 100) if x % 100 == 0 else (50, 50, 50)
    draw.line([(x, 0), (x, container_h)], fill=color, width=1)
    if x % 100 == 0:
        draw.text((x + 2, 5), str(x), fill=(255, 255, 255), font=font)

for y in range(0, container_h, 20):
    color = (100, 100, 100) if y % 100 == 0 else (50, 50, 50)
    draw.line([(0, y), (container_w, y)], fill=color, width=1)
    if y % 100 == 0:
        draw.text((5, y + 2), str(y), fill=(255, 255, 255), font=font)

# Save the grid map
os.makedirs('TotalParking/scratch', exist_ok=True)
canvas.save('TotalParking/scratch/grid_map.png')
print("Successfully generated grid_map.png")
