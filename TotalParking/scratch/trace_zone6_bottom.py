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

print("--- Tracing Red Lines between Zone 6 & Zone 1 (Y from 400 to 700) ---")
for y in range(400, 700, 15):
    red_xs = []
    for x in range(550, 950):
        if is_red_orange(*pixels[x, y]):
            red_xs.append(x)
    if red_xs:
        # Group contiguous
        groups = []
        current_group = [red_xs[0]]
        for val in red_xs[1:]:
            if val - current_group[-1] <= 3:
                current_group.append(val)
            else:
                groups.append(current_group)
                current_group = [val]
        groups.append(current_group)
        group_strs = [f"[{min(g)} to {max(g)}]" for g in groups]
        print(f"Y = {y:3d}: Red pixels at X = " + ", ".join(group_strs))
