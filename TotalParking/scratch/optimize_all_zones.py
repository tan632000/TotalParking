from PIL import Image
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

# Extract red coordinates
red_pts = []
for y in range(container_h):
    for x in range(container_w):
        if is_red_orange(*pixels[x, y]):
            red_pts.append((y, x))
red_pts = np.array(red_pts)

# Define the original polygons for all 6 zones
original_polygons = {
    1: [(565,575), (590,575), (950,540), (950,730), (670,770), (565,780)],
    2: [(220,365), (280,365), (280,480), (350,480), (350,470), (520,470), (590,470), (590,575), (565,575), (565,780), (560,780)],
    3: [(15,150), (150,15), (315,15), (280,250), (250,250), (220,365)],
    4: [(315,15), (520,15), (520,470), (350,470), (350,480), (280,480), (280,365), (220,365), (250,250), (280,250)],
    5: [(520,15), (950,15), (950,235), (720,235), (630,185), (520,185)],
    6: [(520,185), (630,185), (720,235), (950,235), (950,540), (590,575), (590,485), (520,485)]
}

# 1. Identify all unique vertices
unique_vertices = []
for zone_id, pts in original_polygons.items():
    for pt in pts:
        if pt not in unique_vertices:
            unique_vertices.append(pt)

# 2. Map polygon vertices to indices of unique_vertices
polygon_indices = {}
for zone_id, pts in original_polygons.items():
    polygon_indices[zone_id] = [unique_vertices.index(pt) for pt in pts]

print(f"Total unique vertices: {len(unique_vertices)}")

# 3. Define the evaluation function
def evaluate_all(vertices):
    total_loss = 0.0
    for zone_id, indices in polygon_indices.items():
        pts = [vertices[idx] for idx in indices]
        # Sample points along the polygon boundary
        sampled_pts = []
        for i in range(len(pts)):
            p1 = pts[i]
            p2 = pts[(i + 1) % len(pts)]
            for t in np.linspace(0, 1, 8):  # 8 points per segment for speed
                x = p1[0] + t * (p2[0] - p1[0])
                y = p1[1] + t * (p2[1] - p1[1])
                sampled_pts.append((y, x))
        
        sampled_pts = np.array(sampled_pts)
        zone_loss = 0.0
        for pt in sampled_pts:
            dists_sq = np.sum((red_pts - pt)**2, axis=1)
            zone_loss += np.sqrt(np.min(dists_sq))
        total_loss += zone_loss / len(sampled_pts)
    return total_loss

# 4. Perform coordinate descent on unique_vertices
current_vertices = list(unique_vertices)

# Initial shifts: let's apply the rough dx=60, dy=30 shift to give it a good start
# (except for left-most edges which have smaller shifts, but optimization will refine it)
for idx in range(len(current_vertices)):
    x, y = current_vertices[idx]
    current_vertices[idx] = (x + 60, y + 30)

# We also know the optimized points for Zone 3, so let's pre-initialize them:
# Zone 3 old: [(15,150), (150,15), (315,15), (280,250), (250,250), (220,365)]
# Zone 3 optimized: [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
zone3_old = [(15,150), (150,15), (315,15), (280,250), (250,250), (220,365)]
zone3_opt = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
for old, opt in zip(zone3_old, zone3_opt):
    idx = unique_vertices.index(old)
    current_vertices[idx] = opt

print("Initial Total Loss:", evaluate_all(current_vertices))

# Run optimization loops
improved = True
iteration = 0
while improved and iteration < 5:
    improved = False
    iteration += 1
    print(f"Iteration {iteration}...")
    for idx in range(len(current_vertices)):
        best_pt = current_vertices[idx]
        best_loss = evaluate_all(current_vertices)
        
        # Search neighborhood of the vertex
        for dx in range(-6, 7, 3):
            for dy in range(-6, 7, 3):
                if dx == 0 and dy == 0:
                    continue
                test_vertices = list(current_vertices)
                test_vertices[idx] = (current_vertices[idx][0] + dx, current_vertices[idx][1] + dy)
                loss = evaluate_all(test_vertices)
                if loss < best_loss:
                    best_loss = loss
                    best_pt = test_vertices[idx]
                    improved = True
        current_vertices[idx] = best_pt

# Fine search
for idx in range(len(current_vertices)):
    best_pt = current_vertices[idx]
    best_loss = evaluate_all(current_vertices)
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            test_vertices = list(current_vertices)
            test_vertices[idx] = (current_vertices[idx][0] + dx, current_vertices[idx][1] + dy)
            loss = evaluate_all(test_vertices)
            if loss < best_loss:
                best_loss = loss
                best_pt = test_vertices[idx]
    current_vertices[idx] = best_pt

# 5. Output optimized polygons
print("\n=== OPTIMIZED POLYGONS ===")
for zone_id, indices in polygon_indices.items():
    pts = [current_vertices[idx] for idx in indices]
    pts_str = " ".join([f"{int(x)},{int(y)}" for x, y in pts])
    print(f"Zone {zone_id}: {pts_str}")
