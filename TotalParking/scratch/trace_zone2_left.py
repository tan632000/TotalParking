from PIL import Image
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

# Scan at different Y levels to find the X coordinate of the diagonal left wall
print("--- Tracing Left Diagonal Wall of Zone 2 ---")
for y in range(400, 730, 25):
    xs = []
    for x in range(250, 600):
        if is_red_orange(*pixels[x, y]):
            xs.append(x)
    if xs:
        # We expect the diagonal wall to be the left-most red line in this region
        # (excluding internal parking lines)
        leftmost_red = min(xs)
        print(f"Y = {y:3d}: Leftmost red pixel at X = {leftmost_red}")
