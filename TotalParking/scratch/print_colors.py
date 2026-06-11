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

print("--- Printing RGB values around X=380, Y=45-75 ---")
for y in range(40, 80, 5):
    row_strs = []
    for x in range(370, 395, 3):
        r, g, b = pixels[x, y]
        row_strs.append(f"({x},{y}):{r},{g},{b}")
    print(" | ".join(row_strs))
