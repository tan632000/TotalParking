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

# Yellow threshold: high R, G and relatively low B
def is_yellow(r, g, b):
    return r > 200 and g > 170 and b < 130

print("--- Tracing Yellow Corridor in the Right Side (Y from 400 to 650) ---")
for y in range(400, 650, 15):
    yellow_xs = []
    for x in range(500, 950):
        if is_yellow(*pixels[x, y]):
            yellow_xs.append(x)
    if yellow_xs:
        # Group contiguous
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
        print(f"Y = {y:3d}: Yellow pixels at X = " + ", ".join(group_strs))
