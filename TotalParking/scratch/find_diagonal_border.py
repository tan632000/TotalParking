from PIL import Image
import numpy as np

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

# We want to trace the diagonal red line.
# Let's scan Y from 400 to 700, and for each Y, find the red pixels that are part of the diagonal wall.
# Since the diagonal wall goes from the vertical line (around X=573 or X=602) to the right-most edge (X=923).
# Let's print the red pixels in the region X: 573 to 923, Y: 450 to 650.
print("--- Finding Diagonal Border ---")
diagonal_pts = []
for y in range(450, 650):
    for x in range(573, 923):
        if is_red_orange(*pixels[x, y]):
            # Filter out the vertical lines at X=573 and X=602, and right edge X=923
            if x != 573 and x != 602 and x != 923:
                # Let's check if this red pixel belongs to a diagonal line
                diagonal_pts.append((x, y))

# Let's group and fit a line
if diagonal_pts:
    pts = np.array(diagonal_pts)
    # We expect the diagonal border to have Y decreasing as X increases (old: 590,575 -> 950,540)
    # Let's filter points that might be noise
    # We will fit a line using RANSAC or a simple linear regression on a filtered set of points.
    # Let's print the red pixels for a few X columns to find the Y values
    for x in [600, 650, 700, 750, 800, 850, 900]:
        ys = [pt[1] for pt in diagonal_pts if pt[0] == x]
        if ys:
            print(f"At X={x}: Y range = {min(ys)} to {max(ys)}, mean = {np.mean(ys):.1f}")
