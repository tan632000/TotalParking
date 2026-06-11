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

# Let's print out all red pixels in the bottom area (Y > 700) to find the exact boundary
print("--- Tracing all red pixels in bottom region Y > 700 ---")
red_by_y = {}
for y in range(700, 781):
    for x in range(500, 950):
        if is_red_orange(*pixels[x, y]):
            if y not in red_by_y:
                red_by_y[y] = []
            red_by_y[y].append(x)

for y in sorted(red_by_y.keys()):
    xs = red_by_y[y]
    # group xs
    groups = []
    current_group = [xs[0]]
    for val in xs[1:]:
        if val - current_group[-1] <= 4:
            current_group.append(val)
        else:
            groups.append(current_group)
            current_group = [val]
    groups.append(current_group)
    group_strs = [f"[{min(g)} to {max(g)}]" for g in groups]
    print(f"Y = {y}: Red at X = " + ", ".join(group_strs))
