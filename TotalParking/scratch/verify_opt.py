from PIL import Image, ImageDraw
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

# Draw the optimized polygon
pts = [(57,152), (185,46), (343,45), (308,248), (278,293), (308,390)]
draw = ImageDraw.Draw(canvas)
# Semi-transparent fill like in HTML (e.g. orange overlay)
overlay = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
draw_overlay = ImageDraw.Draw(overlay)
draw_overlay.polygon(pts, fill=(249, 115, 22, 100), outline=(249, 115, 22, 255))
canvas_rgba = canvas.convert('RGBA')
final_img = Image.alpha_composite(canvas_rgba, overlay)

os.makedirs('TotalParking/scratch', exist_ok=True)
final_img.save('TotalParking/scratch/zone3_optimized_verify.png')
print("Successfully generated zone3_optimized_verify.png")
