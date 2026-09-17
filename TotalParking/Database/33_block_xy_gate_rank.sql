-- Toa do 112 block tu ban ve, va gate_rank suy tu hinh hoc.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi UPDATE/INSERT -> tai khoan ung dung chay duoc.
--
-- ======================= VI SAO =======================
-- gate_rank la thu tu gan -> xa tinh tu cong vao, va la tieu chi xep hang CHINH
-- cua ZoneRouter:
--     .OrderBy(z => z.GateRank).ThenBy(z => z.UsedRatio)
--
-- Truoc file nay ca 6 zone deu co gate_rank = 0, nen dong OrderBy khong phan
-- biet duoc gi va thuc te chi con ThenBy quyet dinh: bo dinh tuyen chon zone
-- RONG NHAT chu khong phai zone GAN NHAT. No chay, nhung khong lam dung viec.
--
-- ======================= LAY TOA DO TU DAU =======================
-- Ban ve IP_led_guide.pdf co nhan so block dat ngay tren tung block. Loc theo
-- CO CHU la ra sach:
--     fs = 150.3  ->  112 nhan, 112 gia tri khac nhau, tu 1 den 112
--     fs = 182.1  ->   13 nhan, chi 10 gia tri  (so kich thuoc, co trung lap)
--
-- Dung 112 nhan cho 112 block, khong thieu khong thua. Lan thu truoc khong loc
-- co chu nen nhat nham ca so kich thuoc, va ket qua ra rac: zone 3 trai tu
-- y=5258 den y=21326. Sau khi loc, moi zone la mot cum gon.
--
-- Toa do luu theo he cua ban ve (don vi PDF), KHONG doi sang mm. Ly do: chi can
-- dung de ve tuong doi tren so do va tinh khoang cach so sanh, ma smallint chi
-- chua toi 32767 -- doi sang mm (toi 224100) se tran.
--
-- Ty le neu can doi: kich thuoc tong 224100 mm trai tren 21591 don vi PDF
--     ~ 10.38 mm / don vi PDF
--
-- ======================= XEP HANG CO CHAC KHONG =======================
-- Khach cho biet ram 1 phia DUOI ban ve la loi vao. Zone 1 nam thap nhat
-- (block 110, 111, 112 o day mat bang).
--
-- Da thu BA vi tri cong khac nhau -- block thap nhat, trong tam zone 1, goc duoi
-- mat bang -- va ca ba cho ra CUNG MOT thu tu. Nen vi tri ram chinh xac khong
-- anh huong toi xep hang, chi anh huong con so met tuyet doi.
--
-- ======================= HAN CHE =======================
-- Day la khoang cach DUONG CHIM BAY, khong phai quang duong lai xe. Ban do cho
-- thay mang lan xe co mui ten mot chieu o nhieu doan, nen mot zone gan theo
-- duong thang van co the xa theo duong di.
--
-- Zone 4 va zone 5 chi chenh nhau 8 m -- thu tu giua hai zone nay co the dao
-- neu tinh theo lan. Khach da chap nhan: "chi la uoc luong tuong trung tren map
-- nen co the khong can chinh xac tuyet doi" (17/09).
--
-- Muon chinh xac thi phai tach mang lan vang tu anh ban do roi tinh duong di
-- thuc. De sau; xep hang hien tai da tot hon han moi thu deu bang 0.

USE total_parking;

-- ------------------------------------------------- gate_rank
UPDATE zone SET gate_rank = CASE zone_id
    WHEN 1 THEN 0   --   0 m, 16 block, 126 o
    WHEN 2 THEN 1   --  82 m, 37 block, 211 o
    WHEN 6 THEN 2   -- 121 m,  8 block,  55 o
    WHEN 5 THEN 3   -- 160 m, 16 block, 109 o
    WHEN 4 THEN 4   -- 168 m, 23 block, 152 o
    WHEN 3 THEN 5   -- 200 m, 12 block, 102 o
    ELSE gate_rank END
WHERE zone_id BETWEEN 1 AND 6;

-- ------------------------------------------------- toa do 112 block
-- Dung bang DAN XUAT thay vi bang tam: tai khoan ung dung khong co quyen
-- CREATE TEMPORARY TABLES, ma migration nay phai chay duoc bang chinh no.
UPDATE block b
JOIN (
    SELECT 1 AS block_no, 12717 AS x, 25559 AS y
    UNION ALL SELECT 2, 13699, 25560
    UNION ALL SELECT 3, 15642, 25537
    UNION ALL SELECT 4, 16639, 25534
    UNION ALL SELECT 5, 11616, 24237
    UNION ALL SELECT 6, 12818, 23122
    UNION ALL SELECT 7, 16249, 23887
    UNION ALL SELECT 8, 17280, 23881
    UNION ALL SELECT 9, 18941, 24209
    UNION ALL SELECT 10, 20284, 24222
    UNION ALL SELECT 11, 20925, 24241
    UNION ALL SELECT 12, 22273, 24248
    UNION ALL SELECT 13, 23398, 23456
    UNION ALL SELECT 14, 22276, 23271
    UNION ALL SELECT 15, 20887, 23278
    UNION ALL SELECT 16, 20243, 23284
    UNION ALL SELECT 17, 18904, 23265
    UNION ALL SELECT 18, 18888, 22450
    UNION ALL SELECT 19, 20256, 22277
    UNION ALL SELECT 20, 20289, 21313
    UNION ALL SELECT 21, 22503, 21289
    UNION ALL SELECT 22, 23498, 21298
    UNION ALL SELECT 23, 24452, 21285
    UNION ALL SELECT 24, 26393, 21300
    UNION ALL SELECT 25, 24439, 19120
    UNION ALL SELECT 26, 23484, 19183
    UNION ALL SELECT 27, 23397, 17225
    UNION ALL SELECT 28, 22307, 17238
    UNION ALL SELECT 29, 21533, 19332
    UNION ALL SELECT 30, 19549, 19531
    UNION ALL SELECT 31, 18702, 19558
    UNION ALL SELECT 32, 18596, 21272
    UNION ALL SELECT 33, 17439, 21248
    UNION ALL SELECT 34, 16581, 21242
    UNION ALL SELECT 35, 15771, 21229
    UNION ALL SELECT 36, 13505, 21362
    UNION ALL SELECT 37, 12705, 21373
    UNION ALL SELECT 38, 11652, 21352
    UNION ALL SELECT 39, 10700, 21015
    UNION ALL SELECT 40, 8573, 21313
    UNION ALL SELECT 41, 7713, 21313
    UNION ALL SELECT 42, 6740, 21326
    UNION ALL SELECT 43, 5754, 21320
    UNION ALL SELECT 44, 6740, 19555
    UNION ALL SELECT 45, 7727, 19202
    UNION ALL SELECT 46, 8733, 19222
    UNION ALL SELECT 47, 9699, 19229
    UNION ALL SELECT 48, 10806, 19322
    UNION ALL SELECT 49, 11666, 19329
    UNION ALL SELECT 50, 12645, 19329
    UNION ALL SELECT 51, 13492, 19322
    UNION ALL SELECT 52, 15614, 19538
    UNION ALL SELECT 53, 16583, 19552
    UNION ALL SELECT 54, 17437, 19545
    UNION ALL SELECT 55, 15588, 18522
    UNION ALL SELECT 56, 16590, 18516
    UNION ALL SELECT 57, 17444, 18529
    UNION ALL SELECT 58, 18755, 18549
    UNION ALL SELECT 59, 19529, 18549
    UNION ALL SELECT 60, 17111, 16542
    UNION ALL SELECT 61, 19539, 16535
    UNION ALL SELECT 62, 19344, 15555
    UNION ALL SELECT 63, 21519, 14611
    UNION ALL SELECT 64, 23477, 14849
    UNION ALL SELECT 65, 24462, 14838
    UNION ALL SELECT 66, 24467, 13154
    UNION ALL SELECT 67, 23466, 12827
    UNION ALL SELECT 68, 22475, 13154
    UNION ALL SELECT 69, 21517, 13133
    UNION ALL SELECT 70, 19550, 14611
    UNION ALL SELECT 71, 18565, 14611
    UNION ALL SELECT 72, 17570, 14589
    UNION ALL SELECT 73, 16585, 14632
    UNION ALL SELECT 74, 15611, 14611
    UNION ALL SELECT 75, 14627, 14611
    UNION ALL SELECT 76, 12656, 14917
    UNION ALL SELECT 77, 12645, 15592
    UNION ALL SELECT 78, 11709, 16538
    UNION ALL SELECT 79, 10882, 15711
    UNION ALL SELECT 80, 14641, 13147
    UNION ALL SELECT 81, 15574, 13147
    UNION ALL SELECT 82, 17580, 13009
    UNION ALL SELECT 83, 18524, 13136
    UNION ALL SELECT 84, 20517, 13139
    UNION ALL SELECT 85, 20553, 12481
    UNION ALL SELECT 86, 21632, 11159
    UNION ALL SELECT 87, 18405, 10872
    UNION ALL SELECT 88, 17427, 10971
    UNION ALL SELECT 89, 16570, 10976
    UNION ALL SELECT 90, 15747, 10993
    UNION ALL SELECT 91, 15574, 12532
    UNION ALL SELECT 92, 17588, 12511
    UNION ALL SELECT 93, 18524, 12521
    UNION ALL SELECT 94, 18690, 9852
    UNION ALL SELECT 95, 18635, 8963
    UNION ALL SELECT 96, 21639, 10521
    UNION ALL SELECT 97, 20689, 8809
    UNION ALL SELECT 98, 21524, 8776
    UNION ALL SELECT 99, 22457, 8765
    UNION ALL SELECT 100, 23448, 8787
    UNION ALL SELECT 101, 24382, 8787
    UNION ALL SELECT 102, 25216, 8809
    UNION ALL SELECT 103, 22270, 10507
    UNION ALL SELECT 104, 21482, 7007
    UNION ALL SELECT 105, 22368, 7049
    UNION ALL SELECT 106, 23309, 7000
    UNION ALL SELECT 107, 25393, 7028
    UNION ALL SELECT 108, 26397, 7042
    UNION ALL SELECT 109, 27345, 7021
    UNION ALL SELECT 110, 24389, 5153
    UNION ALL SELECT 111, 23441, 5130
    UNION ALL SELECT 112, 22483, 5141
) t ON t.block_no = b.block_no
SET    b.origin_x = t.x, b.origin_y = t.y;

-- ------------------------------------------------------------------ doi chieu
SELECT z.zone_id, z.code, z.gate_rank,
       COUNT(b.block_id)  AS so_block,
       SUM(b.slot_count)  AS so_o,
       ROUND(AVG(b.origin_x)) AS tam_x,
       ROUND(AVG(b.origin_y)) AS tam_y
FROM   zone z LEFT JOIN block b ON b.zone_id = z.zone_id AND b.block_no <= 112
GROUP  BY z.zone_id, z.code, z.gate_rank
ORDER  BY z.gate_rank;

SELECT COUNT(*) AS block_co_toa_do FROM block WHERE origin_x IS NOT NULL AND block_no <= 112;
SELECT COUNT(DISTINCT gate_rank) AS so_hang_khac_nhau FROM zone;
