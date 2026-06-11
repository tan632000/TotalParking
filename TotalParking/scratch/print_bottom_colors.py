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

print("--- Printing colors in range Y=[740, 775], X=[500, 750] ---")
non_bg = []
for y in range(740, 775):
    for x in range(500, 750):
        r, g, b = pixels[x, y]
        # If it's not the padding (15, 23, 42)
        if not (abs(r - 15) < 5 and abs(g - 23) < 5 and abs(b - 42) < 5):
            non_bg.append((x, y, (r, g, b)))

print(f"Total non-background pixels: {len(non_bg)}")
# Print the 30 bottom-most non-background pixels
for pt in sorted(non_bg, key=lambda p: p[1], reverse=True)[:30]:
    print(f"X = {pt[0]}, Y = {pt[1]}, RGB = {pt[2]}")
