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
    # Detect red/orange line color
    return (r > 130 and g < 90 and b < 90) or (r > 150 and 50 < g < 130 and b < 80)

# Let's gather red/orange pixels in specific rectangular regions corresponding to the 4 walls of Zone 3
# 1. Top horizontal wall: Y around 40-50, X from 200 to 380
top_wall_pts = []
for x in range(230, 390):
    for y in range(40, 55):
        if is_red_orange(*pixels[x, y]):
            top_wall_pts.append((x, y))

# 2. Left diagonal wall: X from 70 to 240, Y from 40 to 140
left_wall_pts = []
for x in range(70, 240):
    for y in range(40, 140):
        if is_red_orange(*pixels[x, y]):
            left_wall_pts.append((x, y))

# 3. Bottom diagonal wall: X from 70 to 280, Y from 120 to 370
bottom_wall_pts = []
for x in range(70, 280):
    for y in range(120, 370):
        # We only want the boundary, which is the bottom-most red line
        if is_red_orange(*pixels[x, y]):
            bottom_wall_pts.append((x, y))

# 4. Right dividing wall with Zone 4:
# In the old code it is: 315,15 -> 280,250 -> 250,250 -> 220,365
# In the new image, let's find the vertical/diagonal line segments in the right side of Zone 3
# Let's collect all red pixels in the right boundary area: X from 200 to 400, Y from 45 to 370
right_boundary_pts = []
for y in range(45, 370):
    for x in range(200, 400):
        if is_red_orange(*pixels[x, y]):
            right_boundary_pts.append((x, y))

print("Top wall points count:", len(top_wall_pts))
print("Left wall points count:", len(left_wall_pts))
print("Bottom wall points count:", len(bottom_wall_pts))
print("Right boundary points count:", len(right_boundary_pts))

# Let's write out some coordinates of these lines to inspect their equations
# Fit line equations: Y = A*X + B or X = C*Y + D
def fit_line(pts):
    if len(pts) < 2:
        return None
    pts = np.array(pts)
    x = pts[:, 0]
    y = pts[:, 1]
    # linear fit
    p = np.polyfit(x, y, 1)
    return p

print("\n--- Fitting Top Wall ---")
p_top = fit_line(top_wall_pts)
if p_top is not None:
    print(f"Top Wall: Y = {p_top[0]:.4f} * X + {p_top[1]:.4f}")
    # Since it is horizontal, Y should be constant
    ys = [pt[1] for pt in top_wall_pts]
    print(f"Mean Y: {np.mean(ys):.1f}, range: {min(ys)} to {max(ys)}")

print("\n--- Fitting Left Wall ---")
p_left = fit_line(left_wall_pts)
if p_left is not None:
    print(f"Left Wall: Y = {p_left[0]:.4f} * X + {p_left[1]:.4f}")
    # Intersection of Top and Left wall
    # Y = p_top[0]*X + p_top[1] = p_left[0]*X + p_left[1]
    # X = (p_left[1] - p_top[1]) / (p_top[0] - p_left[0])
    # Let's just use the topmost Y = 45 for top wall:
    # 45 = p_left[0]*X + p_left[1] => X = (45 - p_left[1]) / p_left[0]
    x_intersect = (45 - p_left[1]) / p_left[0]
    print(f"Intersection (Top-Left Corner): X = {x_intersect:.1f}, Y = 45")

# Let's print the actual points of the right boundary by finding the red line in horizontal slices
print("\n--- Right Boundary Path ---")
# Let's scan at various Y levels to see where the right border is
for y in range(45, 370, 20):
    xs = []
    for x in range(200, 400):
        if is_red_orange(*pixels[x, y]):
            xs.append(x)
    if xs:
        print(f"Y = {y}: X range = {min(xs)} to {max(xs)}, mean = {np.mean(xs):.1f}")
        
print("\n--- Left/Bottom Boundary Path ---")
# Let's scan at various Y levels to see where the left/bottom boundary is
for y in range(45, 370, 20):
    xs = []
    for x in range(20, 300):
        if is_red_orange(*pixels[x, y]):
            xs.append(x)
    if xs:
        print(f"Y = {y}: X range = {min(xs)} to {max(xs)}, mean = {np.mean(xs):.1f}")
