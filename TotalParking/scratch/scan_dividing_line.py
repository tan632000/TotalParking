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

print("--- Tracing Dividing Line between Zone 4 & 5 (X from 450 to 600) ---")
for y in range(40, 200, 20):
    red_xs = []
    for x in range(450, 600):
        if is_red_orange(*pixels[x, y]):
            red_xs.append(x)
    if red_xs:
        # filter out the horizontal top wall which spans the entire width at Y=45
        # we only print if the red pixels form a small vertical line component
        # (e.g. range of red pixels in this row is small, <= 5 pixels)
        filtered = [x for x in red_xs if not (y == 40 and x > 550)]
        if filtered:
            print(f"Y = {y:3d}: Red pixels at X = {min(filtered)} to {max(filtered)}")
