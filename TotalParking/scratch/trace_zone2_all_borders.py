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

# Scan horizontal profiles to find horizontal red borders
print("--- Horizontal red borders in Zone 2 area (X from 250 to 600) ---")
for y in range(380, 650, 5):
    reds = []
    for x in range(250, 600):
        if is_red_orange(*pixels[x, y]):
            reds.append(x)
    if reds:
        # Group adjacent X
        groups = []
        current_group = [reds[0]]
        for val in reds[1:]:
            if val - current_group[-1] <= 5:
                current_group.append(val)
            else:
                groups.append(current_group)
                current_group = [val]
        groups.append(current_group)
        if len(groups) > 0:
            group_strs = [f"[{min(g)} to {max(g)}]" for g in groups if len(g) > 2]
            if group_strs:
                print(f"Y = {y}: Red at X = " + ", ".join(group_strs))

print("\n--- Vertical red borders in Zone 2 area (Y from 380 to 650) ---")
for x in range(250, 610, 10):
    reds = []
    for y in range(380, 650):
        if is_red_orange(*pixels[x, y]):
            reds.append(y)
    if reds:
        groups = []
        current_group = [reds[0]]
        for val in reds[1:]:
            if val - current_group[-1] <= 5:
                current_group.append(val)
            else:
                groups.append(current_group)
                current_group = [val]
        groups.append(current_group)
        group_strs = [f"[{min(g)} to {max(g)}]" for g in groups if len(g) > 2]
        if group_strs:
            print(f"X = {x}: Red at Y = " + ", ".join(group_strs))
