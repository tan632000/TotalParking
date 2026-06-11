from PIL import Image
import os

# Load image and replicate container resize/pad
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

# Create a binary image to highlight red/orange colors
# Let's inspect RGB values. Red borders on the map usually have high R and lower G, B.
# We'll try a few thresholds.
mask = Image.new('L', (container_w, container_h), 0)
pixels = canvas.load()
mask_pixels = mask.load()

for x in range(container_w):
    for y in range(container_h):
        r, g, b = pixels[x, y]
        # Red/Orange threshold: high red, moderate green, low blue
        if r > 130 and g < 90 and b < 90:  # Pure Red
            mask_pixels[x, y] = 255
        elif r > 150 and g > 50 and g < 130 and b < 90:  # Orange
            mask_pixels[x, y] = 255

os.makedirs('TotalParking/scratch', exist_ok=True)
mask.save('TotalParking/scratch/red_mask.png')
print("Saved red_mask.png")
