# Cartoon Carton — standalone delivery animation

Bộ thùng carton 3D tạo bằng mesh, chỉnh được dài/rộng/cao, kèm animation bay tới khay bánh đầy → bánh nhảy vào → đóng nắp → lấy đà và bay đi.

## Chạy thử

1. Mở `Demo/CartonDeliveryDemo.unity`.
2. Nhấn Play. Có 4 bánh mẫu, thùng tự chạy chuỗi khoảng 3.66 giây và lặp lại.
3. Nút Replay chạy lại; Reset trả thùng và bánh về trạng thái đầu.
4. Chọn **Carton Delivery** để chỉnh đường bay, thời gian, Squash, Tilt, độ cao bánh nhảy.
5. Chọn con **Carton Visual** để chỉnh Length (X), Width (Z), Height (Y), Thickness và material.

Ngoài Play Mode, chọn Carton Delivery và kéo **Preview timeline** trong Inspector để xem từng thời điểm. **Restore preview** hoặc bỏ chọn để trả pose. Trả preview trước khi lưu scene/prefab. Có thể bấm menu **Tools > Cartoon Carton > Validate Delivery** để chạy các kiểm tra và tạo ảnh preview.

## Tích hợp khi khay full

Kéo `Content/CartonDelivery.prefab` vào scene cần dùng. Prefab này chỉ gồm controller và thùng, không phụ thuộc khay hay logic của project.

```csharp
using CandyBlast.Cartoon;
using UnityEngine;

public class TrayDeliveryExample : MonoBehaviour
{
    [SerializeField] private CartonDeliverySequence delivery;
    [SerializeField] private Transform dockPoint;
    [SerializeField] private Transform[] cakeVisualProxies;

    // Gọi hàm này từ logic khay-full của game.
    public void OnTrayFull()
    {
        delivery.PlayAt(dockPoint.position, cakeVisualProxies);
    }
}
```

`dockPoint` là vị trí đáy thùng khi đáp, nên đặt cạnh khay. Entry Position và Exit Position là tọa độ local theo root Carton Delivery; chỉnh tương ứng với bố cục màn hình. Root có thể được đặt hoặc xoay để đổi hướng toàn bộ hoạt ảnh.

**Cake inputs nên là các visual riêng**: mesh hoặc sprite đại diện bánh, không gắn script gameplay/physics đang hoạt động. Sequence trực tiếp di chuyển visual được truyền vào; không clone, không xóa và không đổi parent. Game tự quyết định lúc ẩn bánh gốc, cập nhật dữ liệu khay và xử lý phần thưởng. Bộ này không tìm hoặc sửa object gameplay nào.

- `Play()` dùng danh sách Cakes và Dock Position trong Inspector.
- `PlayAt(worldDockPosition, cakeVisuals)` dùng vị trí world và danh sách visual từ game.
- `ResetSequence()` hủy lượt đang chạy, trả transform và trạng thái nắp ban đầu.
- `IsPlaying`, `Elapsed`, `TotalDuration` cho phép theo dõi tiến độ.
- `OnPacked` gọi khi đã đóng nắp; `OnCompleted` gọi khi kết thúc bay đi. Có thể gán trong Inspector hoặc `.AddListener(...)`.
- Khi hoàn tất, các visual bánh vẫn ở trong thùng tại vị trí bay đi. Reset hoặc disable controller sẽ trả chúng về vị trí gốc, không destroy. Nếu dùng pool, game quản lý thời điểm ẩn/trả proxy.

Các visual bánh cần pivot ở gần giữa, scale dương; root thùng và parent bánh nên dùng scale đồng đều. Đặt carton làm con trực tiếp của controller như prefab. Giữ khay ổn định trong suốt lượt. Không chạy đồng thời CartonExitAnimation cũ hoặc Animator khác trên cùng thùng.

## Nhịp chuyển động

| Giai đoạn | Chuyển động |
|---|---|
| Arrival | Bay theo cung, nghiêng theo đà, giãn nhẹ và mở nắp |
| Settle | Nhún đáp, co giãn và rung tắt dần |
| Loading | Bánh nhảy lần lượt theo cung, xoay nhẹ, thu vừa lòng thùng; thùng phản ứng lúc nhận bánh |
| Close | Hai cặp nắp đóng lệch nhịp, thân thùng nảy nhẹ |
| Anticipation | Co thân, nghiêng và lùi nhẹ lấy đà |
| Departure | Tăng tốc, giãn thân và nghiêng theo hướng bay |

## Tham số chính

| Nhóm | Tham số |
|---|---|
| Đường bay | Entry Position, Dock Position, Exit Position, Flight Arc |
| Nhịp | Arrival/Settle/Cake Jump/Close/Anticipation/Departure Duration, Cake Stagger |
| Cảm giác cartoon | Squash, Tilt, Cake Jump Height, Cake Spin Turns |
| Kích thước bánh | Packing Scale là tỉ lệ tối đa; tự giảm tiếp nếu cần để vừa thùng |
| Clock | Use Unscaled Time để tiếp tục animation khi game pause |

Lòng thùng được chia ô theo số bánh và chiều dài/rộng. Khi đổi kích thước thùng, nắp và vị trí chứa bánh tự cập nhật ở lượt kế tiếp. Tránh đổi kích thước giữa lượt vì vị trí đóng gói được chốt khi bắt đầu.

## Cấu trúc và export

```text
Assets/CartoonCarton/
  Runtime/        Thùng procedural, delivery controller, exit animation cũ
  Editor/         Inspector, tạo demo, validation, export
  Content/        Prefab thùng, prefab delivery, material, demo mở/đóng cũ
  Demo/           Scene delivery với khay và bánh mẫu, script demo riêng
  Documentation/  Báo cáo kiểm chứng, ảnh preview
  README.md
```

Ba assembly riêng: `CartoonCarton.Runtime`, `CartoonCarton.Demo`, `CartoonCarton.Editor`. Không dùng DOTween, Luna, Match3.Core, uGUI hoặc asset gameplay. Namespace cũ `CandyBlast.Cartoon` được giữ để tương thích code đã dùng.

Xuất lại bằng **Tools > Cartoon Carton > Export Unity Package**. File được ghi vào `Exports/CartoonCarton.unitypackage` ở root project. Tool chỉ export thư mục này và sẽ báo lỗi nếu phát hiện dependency asset bên ngoài. Không export ProjectSettings hoặc package manager manifest.

Import vào project khác bằng **Assets > Import Package > Custom Package** rồi mở scene demo. Môi trường đã kiểm chứng: Unity **6000.0.80f1**, Built-in Render Pipeline. Với URP/HDRP cần chuyển material sang shader tương ứng. Chưa kiểm chứng import ở project sạch, bản Unity khác, hoặc build Playworks/WebGL.

## Phạm vi

Đây là bộ presentation độc lập; không tích hợp tự động vào gameplay. Khay và bánh trong demo chỉ là mẫu 3D dựng từ primitive; không có collider, tính điểm, trigger full hay chuyển scene. Scene gameplay và ProjectSettings không được chỉnh sửa bởi bộ này.
