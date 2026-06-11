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

# Let's sample colors at known coordinates of each zone:
zones_samples = {
    3: [(150, 200), (200, 150), (250, 250)], # Zone 3 (orange)
    4: [(400, 150), (450, 200), (420, 300)], # Zone 4 (purple)
    5: [(750, 100), (800, 80), (650, 120)],  # Zone 5 (blue/cyan)
    6: [(750, 300), (800, 350), (650, 300)],  # Zone 6 (green)
    2: [(350, 450), (400, 450), (330, 520)],  # Zone 2 (pink/grey?)
    1: [(750, 500), (800, 550), (650, 600)]   # Zone 1 (yellow?)
}

print("--- Sampling Colors for Each Zone ---")
for zone_id, pts in zones_samples.items():
    print(f"\nZone {zone_id}:")
    for x, y in pts:
        print(f"  At ({x}, {y}): RGB = {pixels[x, y]}")
