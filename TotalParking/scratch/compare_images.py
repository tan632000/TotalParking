from PIL import Image, ImageChops
import numpy as np
import os

# Load the old image (1016 x 781)
old_img = Image.open('TotalParking/Images/zones_map.png').convert('RGB')

# Load the new image and resize/pad it to 1016 x 781 (matching container rendering)
new_raw = Image.open('TotalParking/Images/zones_map.jpeg')
container_w, container_h = 1016, 781
disp_w = container_w
disp_h = int(new_raw.height * (container_w / new_raw.width))
pad_y = int((container_h - disp_h) / 2.0)

new_resized = new_raw.resize((disp_w, disp_h), Image.Resampling.LANCZOS)
new_img = Image.new('RGB', (container_w, container_h), (15, 23, 42))
new_img.paste(new_resized, (0, pad_y))

# Let's find a template in the old image to match in the new image.
# We'll take a crop of the text "ZONE 3" or similar visual elements.
# Let's check the center of the old image, e.g., around X=400 to 600, Y=300 to 500.
# We will do a simple brute-force template match.
old_arr = np.array(old_img).astype(np.float32)
new_arr = np.array(new_img).astype(np.float32)

# Convert to grayscale for matching
old_gray = old_arr.mean(axis=2)
new_gray = new_arr.mean(axis=2)

# Select a 100x100 template from the old image
# Let's pick a region with high contrast, like around the center of the map (e.g., x=450 to 550, y=350 to 450)
th, tw = 100, 100
ty_start, tx_start = 350, 450
template = old_gray[ty_start:ty_start+th, tx_start:tx_start+tw]

# Brute force search in the new image for the best match
best_score = float('inf')
best_dy, best_dx = 0, 0

# Search range
search_range = 80
for dy in range(-search_range, search_range):
    for dx in range(-search_range, search_range):
        ny = ty_start + dy
        nx = tx_start + dx
        if 0 <= ny < container_h - th and 0 <= nx < container_w - tw:
            crop = new_gray[ny:ny+th, nx:nx+tw]
            score = np.sum((crop - template) ** 2)
            if score < best_score:
                best_score = score
                best_dy = dy
                best_dx = dx

print(f"Best Match Offset: dx = {best_dx}, dy = {best_dy} (score = {best_score})")

# Let's verify by printing some coordinates shifted by this offset
# e.g., if old point is (150, 15), new point would be (150 + dx, 15 + dy)
# Let's save a composite image of old and shifted new image to visually confirm
shifted_new = Image.new('RGB', (container_w, container_h))
# Paste new image shifted back by (best_dx, best_dy)
shifted_new.paste(new_img, (-best_dx, -best_dy))

# Create a blend to see alignment
blend = Image.blend(old_img, shifted_new, 0.5)
blend.save('TotalParking/scratch/blend_verify.png')
print("Saved blend_verify.png")
