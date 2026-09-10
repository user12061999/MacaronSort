# Macaron Factory — level đặt tay

Scene: `Assets/MacaronFactory/MacaronFactory.unity`.

## Chỉnh level

Scene đã gán hai prefab trong `MacaronFactory > Levels`:
- `Levels/Level_01_Packed.prefab`: khay ngang/dọc xếp sát cạnh.
- `Levels/Level_02_Stacked.prefab`: khay nhiều tầng, có khay nghiêng và khay ẩn màu.

Mở prefab bằng Prefab Mode để chỉnh trực tiếp:
1. Chọn `Conveyor Path`, dùng công cụ Spline của Unity để đổi đường đi. Spline mở, điểm đầu là đầu vào, điểm cuối là đầu ra. Giữ đường đi trên mặt phẳng XZ.
2. Trong `MacaronLevel`, chỉnh `Columns` (1–5), `Lane Spacing`, `Row Spacing`, `Stop Before Exit`, `Exit Zone Length`. Bấm **Rebuild conveyor preview** sau khi chỉnh đường hoặc độ rộng, rồi lưu prefab.
3. Di chuyển/xoay các khay dưới `Trays` bằng Move/Rotate. Xoay quanh Y để đặt ngang, dọc hoặc nghiêng. Dùng prefab `Tray_1x4` và `Tray_2x4` hiện có.
4. Trên mỗi khay, đặt `Level Color`, `Stack Layer` (0 là đáy) và `Mystery`. Tầng logic và độ cao Y phải được đặt tương ứng; mẫu đang cách tầng 0,46 đơn vị. Khay cùng tầng đặt sát cạnh; khay tầng trên che phần diện tích thực của khay dưới kể cả khi xoay nghiêng.
5. Di chuyển sáu `Waiting slot` và các tấm bàn để bố cục phù hợp đường đi. Giữ đủ sáu tham chiếu trong `Waiting Slots`. `Counter Anchor` là vị trí bộ đếm bánh.
6. `Camera Tilt` chỉnh góc nhìn. Camera orthographic tự bao trọn các bàn và băng chuyền, chừa chỗ cho HUD và safe area của màn hình.

Nhân bản prefab để thêm level và đưa vào danh sách `Levels` theo thứ tự. Hai mẫu hiện tại lặp lại khi hết danh sách. `Stage Override` trên scene chọn stage cụ thể; 0 dùng tiến độ đã lưu.

Thứ tự bánh được tạo từ màu/sức chứa khay: tầng cao trước, cùng tầng theo thứ tự khay trong Hierarchy. Đổi thứ tự sibling để chỉnh thứ tự cấp bánh. Hiện chưa có danh sách bánh độc lập. `Mystery` được đặt tay theo level; hãy bật ở các màn mong muốn (ví dụ stage 7 và từ stage 10).

## Gameplay và tốc độ

Dùng `ConveyorController` và mesh/spline của package hiện có. Số cột quyết định độ rộng cố định; chỉ hàng cuối có thể thiếu bánh. Chỉ hàng đầu được nhận, từng bánh bay vào đúng Pocket; bánh trước vào khay xong mới nhận bánh tiếp theo. Băng chuyền tiến khi còn khoảng trống, dừng trước endpoint theo `Stop Before Exit` (mặc định 0,5), không loop. Phần hàng chưa có chỗ được giữ ở đầu vào.

Hàng bánh xoay theo tiếp tuyến tại tim spline; các làn tạo cung đồng tâm ở đoạn cua. `Row Spacing` giữ khoảng cách trên đoạn thẳng (tối thiểu bằng kích thước bánh cộng khe 0,015); chỉ đoạn cua được bù khoảng cách theo bán kính mép trong. Không còn giãn toàn bộ tuyến theo cua chật nhất. Hàng đi chậm hơn qua cua và trở lại nhịp ban đầu trên đoạn thẳng. Bản đồ độ cong lấy mẫu một lần khi tải level. Bán kính cua cần lớn hơn nửa bề rộng hàng bánh để làn trong không gập ngược.

Trên `MacaronFactory`:
- `Conveyor Speed`: tốc độ băng chuyền.
- `Macaron Exit Time`: thời gian một bánh bay vào khay (giây), mặc định 0,35. Đặt 0 để dùng tốc độ cũ.
- `Macaron Exit Curve`: tiến độ animation theo thời gian chuẩn hóa 0–1; giữ hai đầu (0,0) và (1,1). Áp dụng cho chuyển động, xoay và thu phóng.
- `Macaron Exit Jump Height`: độ cao nhảy theo đơn vị world, mặc định 0,7; đặt 0 để bỏ độ nhảy.
- `Macaron Exit Speed`: tốc độ bánh bay vào khay, chỉ dùng khi `Macaron Exit Time = 0`.
- `Deadlock Delay`: thời gian kẹt trước khi thua.
- `Unlock Slot Cost`, `Shipping Reward`: giá mở ô và thưởng thắng.

Bốn ô chờ mở sẵn, hai ô mua bằng coin. Khay vào ô tự thu nhỏ và xoay theo hướng ô. Khay đầy đóng nắp rồi gửi đi. Khi các ô mở đều đầy và hàng đầu không có màu nhận được, game xử lý kẹt/thua; không xét màu phía sau để cứu lượt.

## Cấu trúc

- `MacaronLevel`: dữ liệu và tham chiếu bố cục từng prefab.
- `MacaronCameraFrame`: camera bao trọn level và cập nhật theo màn hình.
- `MacaronTray`: Pocket, trạng thái và diện tích chặn của khay có xoay.
- `MacaronConveyorFlow`: hàng đầu, vùng nhận, điểm dừng và hàng đợi.
- `MacaronFactory`: điều phối, ô chờ, animation, HUD và thắng/thua.
- `Editor/MacaronLevelAuthoring`: tạo hai mẫu và Inspector dựng lại preview.

Các thông số sinh khay cũ chỉ dùng khi danh sách `Levels` rỗng. Prefab/model gốc trong `Macaron_Props` được giữ nguyên. Menu **Tools > Macaron Factory > Create Hand-authored Starter Levels** tạo mẫu nếu chưa có và gán lại danh sách hai mẫu vào scene hiện tại.

Theo yêu cầu, lần thay đổi này không chạy test hoặc Play Mode; chỉ import/biên dịch Unity và lưu các asset.
