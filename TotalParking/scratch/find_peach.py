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

# Print RGB values at some coordinates inside Zone 3 (top left)
test_points = [
    (150, 200), (200, 150), (220, 180), (250, 120), (200, 250)
]

print("--- Checking colors inside Zone 3 ---")
for x, y in test_points:
    r, g, b = pixels[x, y]
    print(f"At ({x}, {y}): RGB = ({r}, {g}, {b})")
