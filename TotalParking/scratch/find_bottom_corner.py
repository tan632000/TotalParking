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

# Let's search for light pixels (wall/boundary) or red/orange pixels in the bottom area.
print("--- Scanning for bottom-most red or light-colored pixels ---")
red_pts = []
light_pts = []

def is_red_orange(r, g, b):
    return (r > 130 and g < 90 and b < 90) or (r > 150 and 50 < g < 130 and b < 80)

def is_light(r, g, b):
    return r > 100 and g > 100 and b > 100

for y in range(700, 775):
    for x in range(600, 800):
        r, g, b = pixels[x, y]
        if is_red_orange(r, g, b):
            red_pts.append((x, y, (r, g, b)))
        if is_light(r, g, b):
            light_pts.append((x, y, (r, g, b)))

if red_pts:
    red_sorted = sorted(red_pts, key=lambda p: p[1], reverse=True)
    print("\nBottom-most red/orange pixels:")
    for pt in red_sorted[:15]:
        print(f"X = {pt[0]}, Y = {pt[1]}, RGB = {pt[2]}")
else:
    print("No red/orange pixels found in range Y=[700, 775]!")

if light_pts:
    light_sorted = sorted(light_pts, key=lambda p: p[1], reverse=True)
    print("\nBottom-most light (wall) pixels:")
    for pt in light_sorted[:15]:
        print(f"X = {pt[0]}, Y = {pt[1]}, RGB = {pt[2]}")
else:
    print("No light pixels found in range Y=[700, 775]!")
