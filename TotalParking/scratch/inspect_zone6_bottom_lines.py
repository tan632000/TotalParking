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

# Let's inspect the vertical line around X=600.
# Where is the vertical line at the bottom-left of Zone 6/Zone 2?
# In trace_zone6_bottom.py:
# Y = 400: X = 602
# Y = 460-505: X = 602
# Y = 520-625: X = 573
# Wait!
# Why did it switch from X = 602 (for Y <= 505) to X = 573 (for Y >= 520)?
# Ah! Because the vertical dividing line steps to the left!
# It goes down at X = 602, then steps left to X = 573, then goes down at X = 573!
# Let's check the horizontal step between X = 602 and X = 573 around Y = 510-520.
print("--- Checking vertical dividing lines X=573 and X=602 ---")
for y in range(480, 540, 5):
    for x in range(565, 615):
        if is_red_orange(*pixels[x, y]):
            print(f"Red pixel at ({x}, {y})")

# Let's check the diagonal line between Zone 6 and Zone 1.
# Old: 590,575 -> 950,540.
# If we shift/scale:
# Bottom-left of this diagonal is at X=602 or X=573?
# Let's scan for red pixels in Y = 500 to 600, X = 600 to 923.
print("\n--- Scanning diagonal red line (Y from 500 to 600) ---")
for y in range(500, 600, 10):
    xs = []
    for x in range(600, 930):
        if is_red_orange(*pixels[x, y]):
            xs.append(x)
    if xs:
        print(f"Y = {y}: red pixels at X = {min(xs)} to {max(xs)}")
