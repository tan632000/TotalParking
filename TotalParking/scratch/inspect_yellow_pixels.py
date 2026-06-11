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

def is_yellow(r, g, b):
    # detect yellow corridor color
    return r > 200 and g > 160 and b < 130

print("--- Tracing Yellow Corridor in lower-left (Y from 450 to 750) ---")
for y in range(450, 750, 15):
    yellow_xs = []
    for x in range(200, 800):
        if is_yellow(*pixels[x, y]):
            yellow_xs.append(x)
    if yellow_xs:
        # Group adjacent X
        groups = []
        current_group = [yellow_xs[0]]
        for val in yellow_xs[1:]:
            if val - current_group[-1] <= 5:
                current_group.append(val)
            else:
                groups.append(current_group)
                current_group = [val]
        groups.append(current_group)
        group_strs = [f"[{min(g)} to {max(g)}]" for g in groups]
        print(f"Y = {y}: Yellow pixels at X = " + ", ".join(group_strs))
