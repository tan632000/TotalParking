from PIL import Image, ImageDraw
import numpy as np
import os

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

# Create a binary mask of red pixels
mask = np.zeros((container_h, container_w), dtype=np.uint8)
for y in range(container_h):
    for x in range(container_w):
        if is_red_orange(*pixels[x, y]):
            mask[y, x] = 255

# Let's run a simple connected components or contour finding using opencv
import cv2
contours, hierarchy = cv2.findContours(mask, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)

print(f"Total contours found: {len(contours)}")
# For contours in the lower half, let's print their bounding boxes and simplified polygons
for idx, c in enumerate(contours):
    x, y, w, h = cv2.boundingRect(c)
    if y > 350 or y + h > 350:
        area = cv2.contourArea(c)
        if area > 10:  # ignore tiny noise
            epsilon = 0.02 * cv2.arcLength(c, True)
            approx = cv2.approxPolyDP(c, epsilon, True)
            pts = approx.reshape(-1, 2)
            pts_str = " ".join([f"{p[0]},{p[1]}" for p in pts])
            print(f"Contour {idx}: bounding box x={x}, y={y}, w={w}, h={h}, area={area}, pts={pts_str}")
