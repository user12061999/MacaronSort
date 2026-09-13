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

Băng chuyền chạy theo `Conveyor Speed` và nút x1/x2 trên HUD. Đã bỏ tăng tốc tự động khi khớp màu hoặc khi bánh bay vào khay, cùng hai biến `Matching Conveyor Multiplier` / `Matching Speed Transition`.

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

Factory giữ các tham chiếu conveyor/level/bánh, scale bánh, tốc độ băng chuyền, animation bánh/khay, độ tối khay bị che, Stage Override và cân bằng coin/thua. `Macaron Exit Speed` vẫn cần khi `Macaron Exit Time=0`; không phải biến thừa. Các thông số animation và cấu hình level đã lưu được giữ nguyên.

## Đóng khay vào carton

Khi bánh cuối cùng đáp xuống, khay nảy xong rồi được đóng vào carton: hộp bay tới → cả khay cùng bánh nhảy vào → đóng nắp → hộp bay đi. Ô chờ chỉ được giải phóng và số khay shipped chỉ tăng sau khi animation hoàn tất. Mỗi khay đầy có sequence đóng hộp riêng, chạy đồng thời mà không chờ hộp trước gửi xong; các ô khác vẫn nhận bánh bình thường. Pause dừng cả animation đóng hộp.

Scene đã gắn `Prefabs/CartonDelivery.prefab` tại **MacaronFactory > Carton shipping > Carton Delivery Prefab**. `Carton Dock Offset` chỉnh vị trí đáy hộp so với khay (mặc định X=0, Y=0, Z=-1,2: hộp nằm phía Z âm của khay, cùng độ cao). Kích thước hộp tự theo bounds khay và bánh, hỗ trợ cả 1×4 và 2×4.

Mở prefab carton để chỉnh component **Carton Delivery Sequence**: `Arrival Duration` (0,25s), `Settle Duration` (0,1s), `Cake Jump Duration` (0,35s — thời gian cả khay nhảy vào hộp), `Close Duration` (0,3s), `Anticipation Duration` (0,1s), `Departure Duration` (0,45s). `Cake Jump Height`, `Packing Scale`, `Squash`, `Tilt`, `Entry Position` và `Exit Position` điều khiển chuyển động. Entry/Exit tính tương đối với vị trí khay. Giữ `Play On Start` và `Use Unscaled Time` tắt; runtime truyền vị trí dock và khay, nên `Cakes`, `Cake Stagger` không cần chỉnh.

Game sử dụng bản sao mesh của khay/bánh, giữ material và màu riêng, không đưa component gameplay/collider vào animation. Proxy được ẩn trước khi dọn sequence để tránh `ResetSequence` làm khay hiện lại. Material `Prefabs/FactoryCardboard.mat` được sao từ material URP của package. Trong `CartonDeliverySequence`, số cột được giới hạn theo số vật thể để một khay dài luôn được đặt giữa hộp.

Đã import/compile; chưa chạy test hoặc Play Mode.

### Đường nhảy vào hộp và độ lệch điểm đến

Khay nâng lên cao hơn miệng hộp (có cộng kích thước visual và `Cake Jump Height`), di chuyển qua phía trên miệng hộp rồi hạ thẳng vào trong. Scale và rotation đóng gói hoàn tất trước đoạn hạ xuống để tránh xuyên thành hộp. `Cake Jump Duration` điều khiển tổng thời gian ba đoạn.

`MacaronFactory > Carton Dock Random Range`: biên độ ngẫu nhiên theo X/Z (hai giá trị trong Vector2), mặc định ±0,2 theo X và ±0,25 theo Z, cộng vào `Carton Dock Offset`. Mỗi hộp lấy một điểm cố định khi bắt đầu boxing; Y không đổi. Đặt cả hai bằng 0 để tắt random. Các khay đầy vẫn boxing đồng thời.

## Level 4 — Junction Loop (quay liên tục)

`Levels/Level_04_JunctionLoop.prefab`: vòng giữa quay liên tục, hai nhánh cong cấp bánh, 2 cột, **96 bánh / 12 khay 2×4**. Sáu màu có 16 bánh và hai khay mỗi màu. Khay xếp sát thành 3 cột × 4 hàng. Supply gồm 24 cụm, mỗi cụm 4 bánh, lặp Red/Green/Blue/Yellow/Purple/Orange bốn lần.

Vòng bắt đầu với 100% vị trí hàng có bánh, không chừa ngẫu nhiên 10% vị trí trống. Nếu supply ít hơn sức chứa tính từ spacing, số vị trí được giới hạn theo supply và chia đều quanh vòng; không tạo thêm bánh ngoài config. Phần supply còn lại chia luân phiên sang hai nhánh. Khi một vị trí hàng trống đi qua junction, nhánh có bánh chờ giữ vị trí đó và đưa hàng bánh vào. Vị trí chỉ trống khi mọi bánh trong hàng đã được lấy. Đầu nhánh dừng ngoài miệng nối khi chưa có chỗ; vòng giữa vẫn chạy. Khoảng cách trên vòng tính theo lane phía trong để hạn chế chèn bánh ở cua.

Cổng thu nằm giữa cạnh dưới, sát Waiting Slots, tại T=0/1 và luôn để trống. Đã xóa GameObject Collection gate - waiting slots, giữ opening trên thành vòng. Bánh bay lần lượt theo Macaron Launch Interval khi có khay khớp màu. Chỉ hàng gần cửa nhất trong vùng thu được xét; bánh không khớp tiếp tục quay lại vòng sau. Không dừng trước endpoint như các level băng chuyền mở.

Khi mọi ô chờ đang mở đều bận, chỉ xét thua nếu không còn bánh khớp có thể tới cửa: kiểm tra toàn bộ bánh trên vòng, và chờ nhánh cấp thêm nếu vòng còn vị trí trống. Không báo thua chỉ vì một hàng không khớp vừa đi qua. Pause dừng cả vòng và nhánh; boxing giữ cơ chế hiện tại.

Chỉnh Conveyor Path và hai feeder bằng spline. Đầu cuối mỗi nhánh (Join At=End) phải nằm trên đường tâm vòng. Level này dùng nhánh thẳng vuông góc với đoạn thành thẳng để miệng nối khớp mép; chuyển động nhập vòng được blend. Sync Junction cập nhật miệng nối/mesh; runtime đồng bộ độ rộng theo scale bánh. Giữ cửa thu ở đường nối T=0/1 và các junction ngoài vùng cửa. Collection Gate để trống cho level này. Badge đếm bánh đã chuyển vào tâm vòng.

MacaronLoopFlow quản lý vòng và hàng đợi cấp bánh riêng; MacaronConveyorFlow giữ cơ chế cũ cho level mở. Menu Create Junction Loop Level chỉ tạo khi asset chưa có, không ghi đè level đã chỉnh. Stage Override=4 để xem màn này; đặt 0 để dùng tiến độ lưu.

Đã kiểm tra cấu hình và compile. Chưa chạy test hoặc Play Mode; chuyển động và độ khó chưa được chơi xác nhận.

Khoảng cách bánh trên vòng đã giảm phần đệm từ 12% + 0,015 xuống 2% + 0,005 đơn vị, vẫn tính theo lane trong ở cua. Hai nhánh dùng khoảng cách hàng riêng, không còn dùng khoảng cách lớn của vòng. Row Spacing của level vẫn là giới hạn tối thiểu. Chưa chạy test hoặc Play Mode để xác nhận hình ảnh.

### Config khoảng cách bánh

Mở prefab Level_04_JunctionLoop, chọn component MacaronLevel > Loop cake spacing:
- Loop Spacing Multiplier: hệ số khoảng cách hàng trên vòng, hiện 0,9. Giảm về 0,85 hoặc 0,8 để sát hơn; 1 là khoảng cách tự tính theo góc cua.
- Feeder Spacing Multiplier: hệ số riêng cho hai nhánh, hiện 0,9.

Giá trị áp dụng khi mở lại level/Retry. Vòng chia thành số vị trí nguyên nên thay đổi rất nhỏ có thể chưa tăng số hàng. Hệ số thấp có thể khiến bánh chạm/chồng ở cua; đây là điều chỉnh trực tiếp, không bị clamp ngược về khoảng cách tự động. Lane Spacing vẫn chỉnh khoảng cách ngang giữa các cột (runtime có mức tối thiểu theo kích thước bánh).

12 khay đã được xếp lại theo kích thước collider, khe giữa hai khay khoảng 0,025 đơn vị theo cả X/Z. Hai nhánh đã được chỉnh thẳng và Sync Junction lại, không dùng khoảng overlap âm ở chỗ nối. Chưa chạy test hoặc Play Mode để xác nhận hình ảnh.
### Auto Scene / Game preview trong Edit Mode

Chọn MacaronLevel (asset prefab hoặc root trong Prefab Mode), bật `Auto scene / game preview` ở đầu Inspector. Sửa thông số sẽ cập nhật bản xem trước sau khoảng 0,3 giây: chiều rộng/cột bánh, khoảng cách vòng/nhánh, supply màu, bố cục khay và camera. Undo/Redo và chỉnh spline/transform khay cũng cập nhật. Game view dùng camera preview riêng; nút `Focus preview in Scene` đưa Scene view tới bản xem trước trong scene chính. Khi đang ở Prefab Mode, Game view vẫn dùng bản preview ở scene chính; thoát Prefab Mode để focus bản đó trong Scene view.

Preview là bản bố cục tĩnh ở Edit Mode, không chạy gameplay/animation hay thay đổi tiến độ. Bánh vòng dùng chung sampler và công thức sức chứa của MacaronLoopFlow; level mở hiển thị bố trí mẫu trên spline. Prefab gốc vẫn lưu các biến bạn chỉnh theo quy trình Unity. Preview không được lưu vào scene/prefab và tự dọn trước khi vào Play Mode hoặc compile; tắt checkbox để trả Game view về camera scene. Lỗi cấu hình hiển thị ngay trong Inspector. Thay đổi trên instance runtime trong Play Mode không tự ghi ngược về asset.

Đã sửa khe tại junction: branch trim theo `beltHalfWidth` thay vì `RimOffset`, vì opening đã bỏ cả bề dày thành. Nhánh nay kéo tới mép mặt belt, không còn dừng ở mép ngoài thành. Hai mesh nhánh và mesh vòng của Level 4 đã được rebuild/lưu lại.

## Sinh level từ cấu hình Editor

Menu `Tools > Macaron Factory > Open Level Generator` mở Inspector của level mẫu. Hoặc chọn MacaronLevel và mở mục `Generate new level` ở cuối Inspector.

- `Level Name`, `Seed`: tên output và seed lặp lại kết quả kích thước/hướng khay, thứ tự trộn supply.
- `Tray Count`: 1–48 khay; `Color Count`: 1–6 màu; `Large Tray Percent`: xác suất dùng khay 2×4, còn lại 1×4.
- `Table Columns`, `Stack Layers`: bố trí ô khay và số lớp; `Mix Vertical Trays`: trộn khay dọc/ngang; `Mystery Under Stacks`: ẩn màu khay có lớp trên.
- `Tray Scale`, `Tray Gap`: scale và khe giữa các ô. Khay được ghép theo footprint collider thực tế, lấp các khoảng trống còn vừa trên mỗi tầng. Các tầng dùng bố cục riêng, không phải cột khay thẳng hàng.
- `Conveyor Columns`, `Loop Spacing Multiplier`, `Feeder Spacing Multiplier`: số cột bánh và khoảng cách. Camera và Waiting Slots lấy từ template; hình băng chuyền chọn chữ nhật hoặc tam giác.
- `Shuffle Supply`: mặc định tắt, supply đi theo khay ở tầng trên trước. Bật sẽ xáo trộn cả batch khay. Tổng bánh mỗi màu luôn khớp tổng Pocket tương ứng; không có bộ giải chứng minh level luôn thắng được.
- `Add To Factory`: thêm asset mới vào cuối Levels của scene hiện tại. `Select As Starting Stage`: chọn Stage Override của level mới nếu bật Add To Factory.

Bấm `Generate, sau đó Save`. Output nằm trong `Assets/MacaronFactory/Levels/Generated/<tên>/`, gồm prefab và mesh riêng; tên trùng tự thêm hậu tố. Template không bị ghi đè. Editor tự chọn asset mới để xem/chỉnh tiếp với Auto Scene / Game preview. Cấu hình công cụ lưu riêng ở UserSettings/MacaronLevelGenerator.asset. Không tự sinh lại khi kéo thanh config; cần bấm nút Generate để tạo kết quả mới.

Mặc định: 18 khay, 6 màu, 60% khay lớn, bàn 3 cột, 1 lớp, khe 0,025, băng chuyền 2 cột. Số bánh thực tế được báo sau khi sinh. Chưa chạy generator như một bài test hoặc chạy Play Mode.

### Cấu hình băng chuyền rút gọn

- `Conveyor Shape`: chỉ còn Rounded Rectangle (chữ nhật bo góc) và Triangle (tam giác bo góc, cạnh đáy hướng về Waiting Slots).
- `Feeders`: số phần tử là số nhánh; đặt Size = 0 để không có feeder.
- Mỗi feeder chỉ còn `Position` (0–1 dọc vòng, 0/1 ở giữa đáy) và `Length` (chiều dài nhánh). Nhánh luôn nối từ phía ngoài, vuông góc với đường tại vị trí chọn.
- Bỏ random hình/kích thước/vị trí, góc tiếp cận, chọn phía, cầu vượt, số vòng xoắn và các hình khác. Seed vẫn dùng cho khay/supply.
- Kích thước vòng và bán kính cua tự tính theo độ rộng belt; khi không có feeder, vòng tự mở rộng để chứa đủ supply. Các config khay, bánh, khoảng cách và hai nút Generate / Save vẫn giữ nguyên.
- Số feeder, Position và Length đã lưu được giữ lại. Shape cũ không còn hỗ trợ được chuyển về chữ nhật khi mở generator. Prefab level đã lưu không bị thay đổi.
### Generate và Save riêng

Generate tạo bản nháp trong bộ nhớ và mở Auto Scene / Game preview, chưa tạo prefab/mesh asset hay thêm vào Factory. Save lưu đúng bản nháp hiện tại vào thư mục Generated; tùy chọn Add To Factory và Select As Starting Stage áp dụng lúc Save. Sau khi sửa config generator, bấm Generate lại để thay bản nháp. Có thể chỉnh MacaronLevel bản nháp rồi Save. Save bị khóa khi chưa có bản nháp hoặc sau khi đã lưu. Bản nháp chưa lưu bị bỏ khi compile/reload script, vào Play Mode hoặc đóng Unity.

### Bán kính cua và khay xếp sát

Corner Radius chỉnh bán kính đường tâm ở các góc băng chuyền. Giá trị nhỏ bị giới hạn trên nửa độ rộng belt 0,1 đơn vị để tránh gập mép trong; góc được giới hạn theo chiều dài cạnh. Vòng không feeder có thể được phóng lớn để đủ supply, làm bán kính cuối tăng theo.

Khay xếp sát theo kích thước collider thật ở mỗi tầng, có tính tâm collider khi xoay. Mix Vertical Trays trộn ngang/dọc; Stack Layers chọn số tầng; Tray Gap chọn khe hở. Mystery Under Stacks dựa trên khay thực sự đè phía trên. Table Columns quyết định chiều rộng vùng xếp; số khay trong từng hàng thay đổi theo kích thước/hướng. Đây là thuật toán xếp gọn, không phải bộ giải tối ưu diện tích hay chứng minh level luôn thắng.
