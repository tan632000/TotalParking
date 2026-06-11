from PIL import Image, ImageDraw
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

# Extract red coordinates
red_pts = []
for y in range(container_h):
    for x in range(container_w):
        if is_red_orange(*pixels[x, y]):
            red_pts.append((y, x))
red_pts = np.array(red_pts)

def evaluate_y(y_base, x_left_val):
    # Test a shape with a step:
    # bottom-right at y_base
    # bottom-middle 1 at y_base
    # bottom-middle 2 at y_base - 30
    # bottom-left at y_base - 30
    pts = [
        (526, 211),  # top-left
        (632, 198),
        (718, 223),
        (923, 235),  # top-right
        (923, y_base),  # bottom-right
        (602, y_base),  # bottom-middle 1
        (602, y_base - 30),  # bottom-middle 2
        (x_left_val, y_base - 30)   # bottom-left
    ]
    
    sampled_pts = []
    for i in range(len(pts)):
        p1 = pts[i]
        p2 = pts[(i + 1) % len(pts)]
        for t in np.linspace(0, 1, 10):
            x = p1[0] + t * (p2[0] - p1[0])
            y = p1[1] + t * (p2[1] - p1[1])
            sampled_pts.append((y, x))
    sampled_pts = np.array(sampled_pts)
    
    total_dist = 0.0
    for pt in sampled_pts:
        dists_sq = np.sum((red_pts - pt)**2, axis=1)
        total_dist += np.sqrt(np.min(dists_sq))
    return total_dist / len(sampled_pts)

print("--- Evaluating Y_base and X_left for Zone 6 ---")
best_loss = float('inf')
best_y = 0
best_x = 0

# Try X_left and Y_bottom
best_loss = float('inf')
best_y = 0
best_x = 0

for x_val in [526, 541, 556, 571, 586, 601]:
    for y_val in [320, 335, 350, 365, 380, 395, 410, 425, 440, 455, 470, 485, 500]:
        loss = evaluate_y(y_val, x_val)
        print(f"X_left = {x_val}, Y_bottom_right = {y_val} -> Loss = {loss:.3f} px")
        if loss < best_loss:
            best_loss = loss
            best_y = y_val
            best_x = x_val

print(f"\nBest configuration: X_left = {best_x}, Y_bottom_right = {best_y} with Loss = {best_loss:.3f} px")
