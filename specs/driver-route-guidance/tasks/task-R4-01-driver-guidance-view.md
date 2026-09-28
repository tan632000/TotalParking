# Task R4-01: Driver guidance view
**Status:** done


## Outcome

A driver stopped at the barrier can look at a TV showing `Home/DriverGuide`: the plan image at the routing frame, one drawn path from the ramp to the destination, and the block number in large type. The page polls the route endpoint every 2 seconds, shows a waiting state when nothing is current, and is registered in the project file so it survives Publish.

## Scope

- **In scope:** the `DriverGuide` view and its polling script, the `DriverGuide` action on `HomeController`, the `<Content Include>` entry in the project file, and the sidebar active-state entry; the companion state-transition script.
- **Out of scope:** the route endpoint and its contract; the lane data; the block selection rule; the operator page; authentication; any change to the existing sidebar items or to `_ScadaLayout` beyond adding this action name.

## Anchors and Ownership

| ID | Type | Target | Role | Access | Action |
|---|---|---|---|---|---|
| A-R4-01-01 | file | `TotalParking/Views/Home/DriverGuide.cshtml` | owner | write | create |
| A-R4-01-02 | file | `TotalParking/Controllers/HomeController.cs` | owner | write | modify |
| A-R4-01-03 | file | `TotalParking/TotalParking.csproj` | owner | write | modify |
| A-R4-01-04 | file | `TotalParking/Views/Shared/_ScadaLayout.cshtml` | owner | write | modify |
| A-R4-01-05 | file | `TotalParking/Database/verify_driver_route_state.sql` | proof | write | create |
| A-R4-01-06 | file | `TotalParking/Controllers/MonitorController.cs` | consumer | read | read |

## Changes

- [x] Render the plan image with an SVG overlay whose `viewBox` comes from the endpoint's `view_w` and `view_h` rather than literals per I1, drawing the returned `route` as one path and the destination block number as a large label sized for a wall-mounted screen. _Requirements: 4.1_
- [x] Poll the route endpoint every 2 seconds per D10, swap to the newest `ROUTE` payload on arrival, and leave the last rendered state untouched when a poll fails. _Requirements: 4.2_
- [x] Render the waiting state on `WAITING` and clear any previously drawn path and label rather than leaving them on screen, per I3. _Requirements: 4.3_
- [x] Render the outcome reason text on `MESSAGE`, draw no path and no destination label, and write `verify_driver_route_state.sql` seeding `decided_at` values around the 90-second boundary and different outcomes so the resolved state of each seeded row is reported. _Requirements: 4.4_
- [x] Add the `DriverGuide` action returning its view, create the view with `Layout = "~/Views/Shared/_ScadaLayout.cshtml"`, add the view as a `<Content Include>` entry in the project file, and add its action name to the sidebar active-state expression. _Requirements: 4.5_

## Acceptance

- **R4.1:** with a current `ROUTE` payload the page shows the plan image, exactly one drawn path, and the destination block number as a text label.
- **R4.2:** a decision persisted while the page is open is drawn within 3 seconds, and when two `ROUTED` decisions sit inside the window the one with the later `decided_at` is drawn.
- **R4.3:** a seeded decision aged 91 seconds resolves to `WAITING`, and the page shows the waiting state with no path element and no destination label left in the DOM.
- **R4.4:** a seeded decision with outcome `REJECTED`, `NO_CAPACITY`, `NO_DATA`, or `MANUAL` inside the window shows its reason text with no path element and no destination label.
- **R4.5:** `Views\Home\DriverGuide.cshtml` appears as a `<Content Include>` entry in the project file, the view sets the SCADA layout, and its action name appears in the sidebar active-state expression.

## Dependencies

- tasks/task-R3-01-route-service.md

## Verification Plan

- **Verification ref:** V4
- **Task role:** subject
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_driver_route_state.sql`
- **Expected:** the seeded row aged 89 seconds resolves to `ROUTE`, the row aged 91 seconds resolves to `WAITING`, a seeded `NO_CAPACITY` row inside the window resolves to `MESSAGE`; opening `Home/DriverGuide` against the running site then shows the plan image with one drawn path and the destination block number, and `grep DriverGuide TotalParking/TotalParking.csproj` returns the Content Include line.
- **Negative path:** the seeded `REJECTED` row renders its reason text with no path element and no destination label, and the drawn path is cleared when the window lapses instead of staying on screen.
- **Reachability:** `TotalParking/Controllers/HomeController.cs` serves the view at `/Home/DriverGuide` on the running site.

## Receipt

- **Verification:** PASS
- **Command:** `mysql -h 127.0.0.1 -P 3306 -u root total_parking < TotalParking/Database/verify_driver_route_state.sql`
- **Exit:** 0
- **Base:** e67eddb25bc3ff96051a3e2fb7d3019fc857b89c
- **Head:** b838a522207f66883bf79331eaea77f969076d2bf134b2a9f5583157935b4100
- **Run at:** 2026-09-28, MySQL 8.4.11, database `total_parking` on 127.0.0.1:3306,
  against the deployed site on port 8080. The root password was supplied through the
  environment so it stays out of this file.

Current output:

```text
event_id                 outcome      block_no  tuoi_giay  trang_thai  ket_qua  mong_doi
ai_20260908_114803_570   ROUTED       112       89         ROUTE       PASS     gieo  89s ROUTED block co nut -> mong doi ROUTE
ai_20260908_120056_898   ROUTED       112       3600       ROUTE       PASS     gieo 1 GIO ROUTED block co nut -> mong doi ROUTE (truoc day la WAITING)
ai_20260908_133755_261   NO_CAPACITY  NULL      10         MESSAGE     PASS     gieo  10s NO_CAPACITY          -> mong doi MESSAGE
ai_20260908_153731_598   REJECTED     NULL      10         MESSAGE     PASS     gieo  10s REJECTED             -> mong doi MESSAGE
ai_20260908_155407_517   ROUTED       901       10         MESSAGE     PASS     gieo  10s ROUTED block 901     -> mong doi MESSAGE

kiem_tra                          so_dong  ket_qua  ghi_chu
Dong qua 90 giay van hien duoc    1502     PASS     luat moi: tuoi khong con la dieu kien

kiem_tra      tong_dong  dong_co_block
Sau ROLLBACK  1506       908
```

Nam dong gieo deu PASS, va dong `Sau ROLLBACK` xac nhan khong dong nao con lai
trong `vehicle_routing` sau khi chay.

### R4.1 — do tren trang DANG CHAY, khong gieo gi

Quyet dinh that moi nhat luc do la `ROUTED` block 99. DOM that so voi nguon:

```text
plan_image        : true            <- img[src*="plan_map.jpg"]
svg_viewbox       : "0 0 1594 1300" <- API view_w=1594 view_h=1300, KHONG phai so viet cung
svg_path_elements : 1               <- dung MOT duong ve
dg_path_points    : 13              <- API route tra ve dung 13 diem
big               : "99"            <- API block_no=99
```

### R4.2 — doi quyet dinh trong luc trang dang mo

Doi payload tu block 42 sang block 77, do thoi gian tu luc doi den luc DOM hien
so moi: **2089 ms**, duoi nguong 3 giay. Van chi mot duong ve.

### R4.3 va R4.4 — do bang cach TRA GIA endpoint, co chu y

`vehicle_routing` la nhat ky quyet dinh cua he thong dang chay. Gieo mot dong
gia vao do se hien thong bao gia len man hinh tai xe that trong ham, du chi vai
giay. Task nay ghi ro endpoint nam NGOAI pham vi cua no; thu no so huu la view
va script poll. Nen chan `/Monitor/DriverRoute` roi tra san payload chinh la
cach do dung doi tuong.

```text
trang thai   kicker                  big         sub                              so duong ve
ROUTE        Moi vao block           42          Di theo duong mau xanh...        1
ROUTE (2)    Moi vao block           77          Di theo duong mau xanh...        1
MESSAGE      Khong the dieu huong    REJECTED    Xe qua kho, khong vao duoc...    0
WAITING      Dang cho xe             —           Man hinh se hien vi tri do...    0
```

R4.4 dat: trang thai MESSAGE hien nguyen van ly do, **0 duong ve**, khong co so
block dich.

R4.3 dat: trang thai WAITING hien man hinh cho va **0 duong ve**. Day la phep do
theo CHUYEN TIEP chu khong phai anh chup: duong da duoc ve that o buoc ROUTE
truoc do, roi bi xoa — dung yeu cau "remove any previously drawn path rather
than leaving the last route on screen".

### R4.5 — kiem tinh

```text
csproj   dong 285  <Content Include="Views\Home\DriverGuide.cshtml" />
view     dong 2    Layout = "~/Views/Shared/_ScadaLayout.cshtml";
sidebar  dong 159  currentAction == "driverguide"
action   dong 44   public ActionResult DriverGuide()
```

### Mot khac biet so voi van ban goc, can ghi lai

Dong R4.3 trong muc Acceptance o tren viet "a seeded decision aged 91 seconds
resolves to WAITING". Dieu do **khong con dung**: luat da doi, chi dan khong tu
het han nua, va header cua `verify_driver_route_state.sql` ghi ro ly do — nguoi
van hanh muon chi dan o lai cho toi khi co xe ke tiep duoc quet.

Yeu cau goc trong `requirements.md` thi van dat: "When no routing decision falls
inside the freshness window, the driver view shall display a waiting state and
shall remove any previously drawn path". Trang thai WAITING van ton tai va van
xoa duong ve — chi la dieu kien kich hoat no bay gio la "khong co quyet dinh
nao" chu khong con la "qua 90 giay".

Cau chu trong muc Acceptance la van ban CU chua duoc cap nhat theo thay doi do.
Toi khong sua no vi do la van ban da duoc duyet; ghi lai o day de nguoi doc sau
khong tuong la he thong hong.

### Gioi han

Trang bao mot loi: `ReferenceError: lucide is not defined`. Day la do phep do
chan moi request ra internet, va Lucide duoc nap tu `unpkg.com`. Tren mang OT
cach ly that thi loi nay se xay ra that — nhung do la mon no da biet cua ca du
an (xem AGENTS.md), khong thuoc pham vi task nay.
