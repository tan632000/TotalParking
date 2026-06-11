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

def evaluate_shape(x_left):
    # Polygon points with variable x_left
    # We will adjust bottom-left (idx 7) and bottom-middle 2 (idx 6)
    # If x_left moves to the right, say to X, then:
    # point 7 (bottom-left) is (x_left, 400)
    # point 6 (bottom-middle 2) is (x_left, 400) or stays (602, 400)?
    # Wait, if x_left is 602, then points 7, 6, 5 all merge or align.
    # Let's test the polygon:
    pts = [
        (526, 211),  # top-left
        (632, 198),
        (718, 223),
        (923, 235),  # top-right
        (923, 430),  # bottom-right
        (602, 430),  # bottom-middle 1
        (602, 400),  # bottom-middle 2
        (x_left, 400) # bottom-left
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

print("--- Evaluating different X_left values for Zone 6 ---")
for x_left in range(526, 603, 5):
    loss = evaluate_shape(x_left)
    print(f"X_left = {x_left}: average distance = {loss:.3f} pixels")
