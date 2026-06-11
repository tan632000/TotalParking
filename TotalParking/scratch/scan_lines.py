from PIL import Image

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
    # Detect red/orange line color
    # Let's be generous: high R, G and B are relatively low
    return r > 120 and g < 90 and b < 90 or (r > 150 and g > 50 and g < 130 and b < 80)

# 1. Scan top wall Y-coordinate for X between 150 and 800
# The top wall should be a horizontal red line near the top of the building
print("--- Scanning Top Wall Y-coordinates ---")
for x in [200, 300, 400, 500, 600, 700]:
    found_y = []
    for y in range(0, 300):
        r, g, b = pixels[x, y]
        if is_red_orange(r, g, b):
            found_y.append(y)
    if found_y:
        # group contiguous values
        print(f"At X={x}: red pixels found at Y range {min(found_y)} to {max(found_y)}")

# 2. Scan Zone 3 / Zone 4 border (vertical-ish line around X=300 to 450)
print("\n--- Scanning Zone 3/4 Border X-coordinates ---")
for y in [50, 100, 150, 200, 250, 300, 350]:
    found_x = []
    for x in range(200, 500):
        r, g, b = pixels[x, y]
        if is_red_orange(r, g, b):
            found_x.append(x)
    if found_x:
        print(f"At Y={y}: red pixels found at X range {min(found_x)} to {max(found_x)}")

# 3. Scan Left Diagonal Wall of Zone 3 (X from 10 to 300)
print("\n--- Scanning Zone 3 Left Wall ---")
for y in [50, 100, 150, 200, 250, 300, 350]:
    found_x = []
    for x in range(0, 300):
        r, g, b = pixels[x, y]
        if is_red_orange(r, g, b):
            found_x.append(x)
    if found_x:
        print(f"At Y={y}: red pixels found at X range {min(found_x)} to {max(found_x)}")
