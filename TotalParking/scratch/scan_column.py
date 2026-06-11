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

# Let's print colors at X = 450, Y from 350 to 530 in steps of 5
print("--- Scan at X = 450 ---")
for y in range(350, 530, 5):
    r, g, b = pixels[450, y]
    # Check if color is red/orange or yellow or white/gray
    color_type = "white/gray"
    if r > 130 and g < 90 and b < 90:
        color_type = "RED"
    elif r > 150 and 50 < g < 130 and b < 80:
        color_type = "ORANGE"
    elif r > 200 and g > 180 and b < 120:
        color_type = "YELLOW"
        
    print(f"Y = {y}: RGB = ({r:3d}, {g:3d}, {b:3d}) -> {color_type}")
