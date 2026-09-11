# Macaron Factory — level đặt tay

Scene: `Assets/MacaronFactory/MacaronFactory.unity`.

## Chỉnh level

Scene đã gán ba prefab trong `MacaronFactory > Levels`:
- `Levels/Level_01_Welcome.prefab`: màn đầu tiên, 24 bánh, 6 khay 1×4, 3 màu và 2 cột bánh.
- `Levels/Level_01_Packed.prefab`: khay ngang/dọc xếp sát cạnh.
- `Levels/Level_02_Stacked.prefab`: khay nhiều tầng, có khay nghiêng và khay ẩn màu.

Mở prefab bằng Prefab Mode để chỉnh trực tiếp:
1. Chọn `Conveyor Path`, dùng công cụ Spline của Unity để đổi đường đi. Spline mở, điểm đầu là đầu vào, điểm cuối là đầu ra. Giữ đường đi trên mặt phẳng XZ.
2. Trong `MacaronLevel`, chỉnh `Columns` (1–5), `Lane Spacing`, `Row Spacing`, `Stop Before Exit`, `Exit Zone Length`. Bấm **Rebuild conveyor preview** sau khi chỉnh đường hoặc độ rộng, rồi lưu prefab.
3. Di chuyển/xoay các khay dưới `Trays` bằng Move/Rotate. Xoay quanh Y để đặt ngang, dọc hoặc nghiêng. Dùng prefab `Tray_1x4` và `Tray_2x4` hiện có.
4. Trên mỗi khay, đặt `Level Color`, `Stack Layer` (0 là đáy) và `Mystery`. Tầng logic và độ cao Y phải được đặt tương ứng; mẫu đang cách tầng 0,46 đơn vị. Khay cùng tầng đặt sát cạnh; khay tầng trên che phần diện tích thực của khay dưới kể cả khi xoay nghiêng.
5. Di chuyển sáu `Waiting slot` và các tấm bàn để bố cục phù hợp đường đi. Giữ đủ sáu tham chiếu trong `Waiting Slots`. `Counter Anchor` là vị trí bộ đếm bánh.
6. Camera dùng **Perspective**. `Camera Tilt` chỉnh góc nghiêng; `Camera Field Of View` mặc định 35 độ. Camera tự chọn khoảng cách gần nhất bao trọn từng phần bàn và băng chuyền. `Camera Padding` là lề safe area theo thứ tự X=trái, Y=phải, Z=dưới, W=trên; mặc định 0,015 / 0,015 / 0,12 / 0,065. Giảm lề để bàn lớn hơn, tăng lề để chừa thêm chỗ cho HUD. FOV thay đổi phối cảnh; khoảng cách camera vẫn tự căn để giữ toàn bộ level trong khung.

Nhân bản prefab để thêm level và đưa vào danh sách `Levels` theo thứ tự. Danh sách hiện tại lặp lại khi hết level. `Stage Override` trên scene chọn stage cụ thể; 0 dùng tiến độ đã lưu.

Khi `Use Custom Macaron Order` tắt, thứ tự bánh được tạo từ màu/sức chứa khay: tầng cao trước, cùng tầng theo thứ tự khay trong Hierarchy. Khi bật, `Macaron Order` là nguồn thứ tự bánh độc lập và không phụ thuộc vị trí, tầng hoặc sibling của khay. `Mystery` được đặt tay theo level; hãy bật ở các màn mong muốn (ví dụ stage 7 và từ stage 10).

## Gameplay và tốc độ

Dùng `ConveyorController` và mesh/spline của package hiện có. Số cột quyết định độ rộng cố định; chỉ hàng cuối có thể thiếu bánh. Chỉ hàng đầu được nhận, từng bánh xuất phát lần lượt vào đúng Pocket theo `Macaron Launch Interval`; có thể có nhiều bánh đang bay. Băng chuyền tiến khi còn khoảng trống, dừng trước endpoint theo `Stop Before Exit` (mặc định 0,5), không loop. Phần hàng chưa có chỗ được giữ ở đầu vào.

Mỗi bánh đi theo làn cong riêng với khoảng cách theo chiều dài làn. Làn ngoài có thể có nhiều bánh trong cùng vùng cua, không sinh thêm bánh. Các bánh cùng nhóm logic được phép lệch nhau trên cua; chúng căn lại theo khoảng cách còn lại ở đoạn thẳng cuối. Đầu vào có chuyển tiếp pha nhẹ để lưới bánh ngay ngắn trước cua. `Row Spacing` là khoảng cách tối thiểu, có thêm khoảng dư nhỏ theo đường kính bánh để tránh chạm nhau ở cung cong. Chỉ nhóm hàng đầu có quyền fill; từng bánh phải tới vùng nhận và cả nhóm phải tới đoạn căn hàng cuối. Hai level mẫu đã kéo dài đoạn thẳng cuối lên 1,05 đơn vị để giữ khoảng dừng 0,5. Khi đặt level mới, chừa đoạn thẳng cuối dài hơn khoảng dừng và bán kính cua lớn hơn nửa bề rộng bánh.

Trên `MacaronFactory`:
- `Conveyor Speed`: tốc độ băng chuyền.
- `Macaron Exit Time`: thời gian một bánh bay vào khay (giây), mặc định 0,35. Đặt 0 để dùng tốc độ cũ.
- `Macaron Exit Curve`: tiến độ animation theo thời gian chuẩn hóa 0–1; giữ hai đầu (0,0) và (1,1). Áp dụng cho chuyển động và xoay; scale có hai pha phóng to rồi thu về Pocket.
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

`Levels` phải có ít nhất một prefab hợp lệ; luồng sinh khay tự động cũ đã bỏ. Prefab/model gốc trong `Macaron_Props` được giữ nguyên. Menu **Tools > Macaron Factory > Create Hand-authored Starter Levels** tạo mẫu nếu chưa có và gán lại các mẫu vào scene hiện tại (Welcome được giữ đầu danh sách nếu đã có).

Theo yêu cầu, lần thay đổi này không chạy test hoặc Play Mode; chỉ import/biên dịch Unity và lưu các asset.

## Animation bánh và khay

Chọn component `MacaronFactory` trong scene:
- **Macaron Exit Scale Multiplier**: tỉ lệ phóng to so với kích thước bánh lúc rời băng chuyền, mặc định 1,25. Scale lên trong 45% thời gian bay, rồi thu về đúng kích thước Pocket trong 55% còn lại.
- **Tray Jump Time / Height / Curve**: thời gian, độ cao và nhịp chuyển động khay lên ô chờ. Mặc định 0,4 giây và 0,75 đơn vị.
- **Tray Waiting Scale**: scale khay khi vào ô chờ, mặc định 0,58.
- **Tray Valid Click Time / Scale**: nhún khi chọn đúng, mặc định 0,1 giây và 0,92 lần kích thước ban đầu; sau đó khay bật lên và thu nhỏ vào ô.
- **Tray Invalid Click Time / Angle**: lắc khi khay bị che hoặc hết ô chờ, mặc định 0,25 giây và 6 độ. Góc xoay đặt tay được giữ sau khi lắc. Bấm liên tục không cộng dồn hiệu ứng.

Ô chờ được giữ ngay khi chọn đúng; khay chỉ nhận bánh sau khi hoàn tất animation lên ô.

## Bố cục được tinh gọn

Hai level mẫu dùng một cua U bán kính 1,1 và một góc rẽ xuống đầu ra ở giữa ô chờ. Bánh trên conveyor đặt 1,6 lần kích thước gốc. Khay trên bàn tăng 15%, bàn ngắn hơn, mẫu xếp chồng chỉ có hai khay nghiêng nhẹ. Lòng khay dùng material riêng màu pastel, không sửa material gốc trong Macaron_Props. Khay ẩn dùng nắp hộp thật với vật liệu giấy đục; nhãn chỉ hiện dấu hỏi hoặc số bánh/sức chứa. Bóng đổ của đèn chính giảm còn 0,28.

Chỉnh sửa lần này được lưu trực tiếp vào hai prefab level và scene; không chạy test hoặc Play Mode.

## Tăng tốc khi đóng khay

`Matching Conveyor Multiplier` (mặc định 2,5) tăng tốc khi hàng đầu có màu khớp khay đang chờ còn chỗ, hoặc đang có bánh bay vào khay. `Matching Speed Transition` (mặc định 0,15 giây) làm mượt chuyển tốc độ. Khay đang di chuyển, đang gửi đi hoặc đã đầy tính cả chỗ được giữ trước không kích hoạt tăng tốc. Hết màu khớp thì trở về tốc độ thường. Hệ số này nhân với tốc độ x1/x2 trên HUD; đặt 1 để tắt tăng tốc tự động. Điểm dừng và điều kiện thua giữ nguyên.

Chỉ hàng đầu được nhận. Trong hàng đó, bánh đủ điều kiện gần đầu ra hơn được xét trước; bánh xuất phát lần lượt theo `Macaron Launch Interval`, không phải đợi bánh trước đáp xuống. Thời gian bay vẫn do `Macaron Exit Time` / `Macaron Exit Speed` điều khiển riêng.

## Khoảng thời gian giữa hai lần bánh nhảy

`MacaronFactory > Macaron exit > Macaron Launch Interval`: khoảng cách giữa thời điểm hai bánh bắt đầu bay, mặc định **0,08 giây**, tối thiểu 0,01 giây. Dùng chung cho mọi khay để giữ thứ tự xuất phát. `Macaron Exit Time` vẫn là thời gian bay của từng bánh; ví dụ thời gian bay 0,35 giây và interval 0,08 giây sẽ có nhiều bánh bay nối tiếp nhau. Mỗi bánh giữ trước một Pocket riêng. Khay chỉ đóng/gửi khi toàn bộ bánh đã đáp xuống; không xử lý thua khi còn bánh đang bay.

## Thứ tự bánh riêng

Mở prefab level, trên `MacaronLevel` bật **Use Custom Macaron Order**. Mỗi phần tử trong **Macaron Order** chứa `Color` và `Count`. Danh sách đọc từ trên xuống, phần tử đầu được phục vụ trước. Dùng Count=1 để đặt từng bánh/màu xen kẽ; nhiều phần tử có thể dùng cùng màu. `Columns` chia chuỗi thành các hàng logic; một cụm màu có thể kéo dài qua nhiều hàng, chỉ hàng cuối được thiếu bánh. Thứ tự này giữ qua Retry, không đảo ngẫu nhiên.

Ví dụ Columns=2: Red×3, Green×1 tạo hàng 1 [Red, Red], hàng 2 [Red, Green]. Bánh lệch hàng khi qua cua chỉ là cách hiển thị theo làn, không đổi thứ tự phục vụ của nhóm.

Inspector báo tổng bánh/số hàng và lỗi khi Count không dương, màu ngoài sáu màu hỗ trợ, danh sách trống hoặc số bánh từng màu khác tổng Pocket của khay cùng màu. Đây là kiểm tra dữ liệu, **không phải bộ giải để xác nhận level có lời giải**. Khi dữ liệu sai, runtime báo lỗi thay vì âm thầm quay về thứ tự sinh từ khay.

## Màn 1 — Welcome

Prefab: `Levels/Level_01_Welcome.prefab`. Hai cột, 24 bánh / 12 hàng logic, 6 khay 1×4 đặt ngang thành hai hàng, không xếp chồng, không khay ẩn, bốn ô chờ mở sẵn. Mỗi màu Red / Green / Blue có hai khay, tổng 8 Pocket.

| Cụm theo thứ tự | Màu | Số bánh |
| --- | --- | --- |
| 1 | Red (Berry) | 4 |
| 2 | Green (Pistachio) | 4 |
| 3 | Blue (Blueberry) | 4 |
| 4 | Red (Berry) | 4 |
| 5 | Green (Pistachio) | 4 |
| 6 | Blue (Blueberry) | 4 |

Đường giải thiết kế: chọn một khay Đỏ, đợi đầy/gửi đi, chọn Xanh lá rồi Xanh dương; lặp lại cho ba khay còn lại. Chỉ cần một ô hoạt động nếu chọn tuần tự, không cần mua ô hoặc dùng coin. Người chơi cũng có thể chuẩn bị các màu tiếp theo ở ô chờ còn trống. HUD hiển thị lời nhắc match màu từ trường `Instruction`.

Scene đang đặt **Stage Override=1** để mở màn này; đặt lại 0 để dùng tiến độ đã lưu. Hai mẫu Packed và Stacked được giữ ở sau Welcome. Menu **Tools > Macaron Factory > Create First Level** tạo màn nếu chưa có, đưa nó lên đầu và chọn Stage Override=1; không ghi đè prefab Welcome đã chỉnh.

Chưa chạy test hoặc Play Mode theo yêu cầu; đường giải trên được thiết kế theo cấu hình, chưa chơi xác nhận trong Unity.

## Khay nảy khi nhận bánh

`MacaronFactory > Tray receive bounce`: `Tray Receive Scale Multiplier` mặc định 1,08; `Tray Receive Bounce Time` mặc định 0,18 giây. Mỗi bánh đáp xuống kích hoạt scale lên rồi xuống. Các lần nhận liên tiếp dùng chung scale nghỉ nên không phóng to cộng dồn. Bánh đang bay bám theo Pocket khi khay nảy, và vẫn kết thúc đúng pose của Pocket.

## Inspector MacaronFactory đã tinh gọn

Đã bỏ `Tray Prefab`, `Tray Prefabs`, `Tray Edge Gap` cùng code bố trí khay theo công thức cũ. Khay đặt trực tiếp trong prefab level. `Columns`, `Row Spacing`, `Lane Spacing`, `Exit Zone Length`, `Stop Before Exit` chỉ chỉnh tại `MacaronLevel`; Factory đọc cấu hình đó và tự tính khoảng cách làn thực tế theo kích thước bánh.

Factory giữ các tham chiếu conveyor/level/bánh, scale bánh, tốc độ và nhịp tăng tốc, animation bánh/khay, độ tối khay bị che, Stage Override và cân bằng coin/thua. `Macaron Exit Speed` vẫn cần khi `Macaron Exit Time=0`; không phải biến thừa. Các thông số animation và cấu hình level đã lưu được giữ nguyên.
