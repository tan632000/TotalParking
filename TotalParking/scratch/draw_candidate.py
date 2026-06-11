from PIL import Image, ImageDraw

# Load the new image and resize/pad it to 1016x781
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

# Create multiple canvases to draw different candidate coordinates
# Candidate 1: Shifted by dx=65, dy=30
c1_canvas = canvas.copy()
c1_draw = ImageDraw.Draw(c1_canvas)
c1_pts = [(15+65, 150+30), (150+65, 15+30), (315+65, 15+30), (280+65, 250+30), (250+65, 250+30), (220+65, 365+30)]
c1_draw.polygon(c1_pts, outline=(255, 165, 0), width=3)

# Candidate 2: Custom tuned coordinates based on intersection analysis
c2_canvas = canvas.copy()
c2_draw = ImageDraw.Draw(c2_canvas)
# Let's adjust slightly:
# Leftmost: (74, 173) -> let's try 74, 173
# Top-left: (205, 45) -> let's try 205, 45
# Top-right: (380, 45) -> let's try 380, 45
# Right bend 1: (345, 280) -> let's try 348, 280
# Right bend 2: (315, 280) -> let's try 318, 280
# Bottom-right: (285, 395) -> let's try 285, 395
c2_pts = [(74, 173), (205, 45), (380, 45), (348, 280), (318, 280), (285, 395)]
c2_draw.polygon(c2_pts, outline=(0, 255, 255), width=3)

# Save both validation images
import os
os.makedirs('TotalParking/scratch', exist_ok=True)
c1_canvas.save('TotalParking/scratch/candidate_shift.png')
c2_canvas.save('TotalParking/scratch/candidate_tuned.png')
print("Saved verification images candidate_shift.png and candidate_tuned.png")
