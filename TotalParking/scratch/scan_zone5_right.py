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
    return (r > 130 and g < 90 and b < 90) or (r > 150 and 50 < g < 130 and b < 80)

print("--- Tracing Red Lines in Right Zone 5 (X from 800 to 1016) ---")
for y in range(20, 260, 20):
    red_xs = []
    for x in range(800, 1016):
        if is_red_orange(*pixels[x, y]):
            red_xs.append(x)
    if red_xs:
        print(f"Y = {y:3d}: Red pixels at X = {min(red_xs)} to {max(red_xs)}")
