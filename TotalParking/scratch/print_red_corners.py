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
    # Detect red/orange line color in zones_map.jpeg
    # Let's inspect the RGB profile. In many drawings, red is pure red (255, 0, 0)
    # or orange (255, 128, 0).
    return (r > 130 and g < 90 and b < 90) or (r > 150 and 50 < g < 130 and b < 80)

vertices_info = [
    {"name": "v1_leftmost", "range_x": (0, 150), "range_y": (80, 220), "desc": "Corner of diagonal wall left side"},
    {"name": "v2_top_left", "range_x": (100, 250), "range_y": (0, 100), "desc": "Top-left corner where diagonal wall meets top wall"},
    {"name": "v3_top_right", "range_x": (250, 400), "range_y": (0, 120), "desc": "Top-right corner where top wall meets vertical dividing wall"},
    {"name": "v4_right_bend1", "range_x": (220, 360), "range_y": (200, 320), "desc": "Where vertical dividing wall bends/steps"},
    {"name": "v5_right_bend2", "range_x": (200, 320), "range_y": (200, 320), "desc": "Where vertical dividing wall steps horizontally"},
    {"name": "v6_bottom_right", "range_x": (160, 280), "range_y": (320, 440), "desc": "Bottom-right corner of Zone 3 (border with Zone 2/4)"},
]

for v in vertices_info:
    rx = v["range_x"]
    ry = v["range_y"]
    
    red_pts = []
    for x in range(rx[0], rx[1]):
        for y in range(ry[0], ry[1]):
            r, g, b = pixels[x, y]
            if is_red_orange(r, g, b):
                red_pts.append((x, y))
                
    print(f"\n--- {v['name']} ({v['desc']}) ---")
    if not red_pts:
        print("No red/orange pixels found in this range!")
        # Let's print average colors in this range to see what they are
        sample_colors = []
        for x in range(rx[0], rx[1], 15):
            for y in range(ry[0], ry[1], 15):
                sample_colors.append(pixels[x, y])
        mean_c = np.mean(sample_colors, axis=0) if sample_colors else [0, 0, 0]
        print(f"Mean RGB in range: {mean_c}")
    else:
        # We want to identify the vertex/corner.
        # Let's print statistics or extreme points depending on the vertex type
        pts_arr = np.array(red_pts)
        
        if v["name"] == "v1_leftmost":
            # Leftmost corner of the building: should minimize X
            idx = np.argmin(pts_arr[:, 0])
            print(f"Leftmost red pixel: {red_pts[idx]}")
        elif v["name"] == "v2_top_left":
            # Top-left corner: should minimize Y
            idx = np.argmin(pts_arr[:, 1])
            print(f"Topmost red pixel: {red_pts[idx]}")
        elif v["name"] == "v3_top_right":
            # Top-right corner of Zone 3: should minimize Y (it's the top wall corner)
            idx = np.argmin(pts_arr[:, 1])
            print(f"Topmost red pixel: {red_pts[idx]}")
        elif v["name"] == "v4_right_bend1":
            # Right bend 1
            # let's list all unique points near the expected region
            print(f"Centroid of red pixels: {np.mean(pts_arr, axis=0)}")
        elif v["name"] == "v5_right_bend2":
            # Right bend 2
            print(f"Centroid of red pixels: {np.mean(pts_arr, axis=0)}")
        elif v["name"] == "v6_bottom_right":
            # Bottom right
            # let's maximize Y or find the bottom-most point
            idx = np.argmax(pts_arr[:, 1])
            print(f"Bottommost red pixel: {red_pts[idx]}")
            
        print(f"Total red pixels found: {len(red_pts)}")
        # Print a few example red pixels
        unique_x = sorted(list(set(pts_arr[:, 0])))
        unique_y = sorted(list(set(pts_arr[:, 1])))
        print(f"X range: {min(unique_x)} to {max(unique_x)}")
        print(f"Y range: {min(unique_y)} to {max(unique_y)}")
