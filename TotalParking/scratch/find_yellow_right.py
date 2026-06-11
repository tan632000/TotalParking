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

def is_yellow(r, g, b):
    return r > 200 and g > 170 and b < 130

print("--- Tracing Yellow Corridor on Right Side (X: 700 to 1016) ---")
for y in range(200, 781, 15):
    yellow_xs = []
    for x in range(700, 1016):
        if is_yellow(*pixels[x, y]):
            yellow_xs.append(x)
    if yellow_xs:
        print(f"Y = {y:3d}: Yellow pixels at X = {min(yellow_xs)} to {max(yellow_xs)}")
