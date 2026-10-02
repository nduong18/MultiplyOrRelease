# Multiply or Release

`Simulation > Simulation Speed` áp dụng ngay trong Play mode, kể cả khi đang Pause; không cần Apply / Restart. Giá trị mới điều khiển mô phỏng, boost, VFX và trails của lượt chơi hiện tại. Chỉnh tốc độ không khởi động lại trận hoặc đổi seed. Thanh Playback > Speed vẫn điều khiển lượt hiện tại cho đến khi Simulation Speed trong config được chỉnh tiếp.

## Hiệu ứng nổ cannon

Đạn đối phương trúng cannon tạo chớp sáng, vòng xung kích, tia lửa/debris màu đội bị trúng và khói nhẹ tại tâm lá cờ. Trúng đạn gây sát thương vẫn có hiệu ứng; phát phá hủy cannon nổ lớn hơn. Hiệu ứng tiếp tục sau khi lá cờ bị ẩn, kể cả khi trận kết thúc. Đạn đội nhà, va chạm grid và thao tác loại đội thủ công không tạo vụ nổ.

Chỉnh `DefaultSimulation > Cannon Impact`: **Enabled**, **Duration**, **Size Multiplier**, **Destruction Multiplier**, **Spark Count / Spark Spread**, **Fire Color**, **Use Team Color**, **Smoke Color / Smoke Opacity**, **Sorting Order** và **Max Bursts**. Pause/Speed/Step điều khiển hiệu ứng; Restart dọn hết vụ nổ. Các đối tượng được tái sử dụng và giới hạn số lượng; ngẫu nhiên hình ảnh không thay đổi seed gameplay. Trong Play mode bấm Apply / Restart sau khi chỉnh.

## Giới hạn va chạm border của đạn

`DefaultSimulation > Projectile > Max Border Collisions` mặc định **10**: từng viên đạn bật lại tối đa 10 lần ở border và biến mất ở lần chạm border thứ **11**. `0` tắt giới hạn. Chạm góc đồng thời ở hai trục tính một lần. Bộ đếm riêng cho mỗi viên và được đặt lại khi đạn mới dùng lại pool. Chạm grid/cannon/boost không tăng bộ đếm border; Despawn On Capture vẫn bật mặc định, nên chạm grid đối phương vẫn chiếm ô rồi biến mất ngay. Lifetime và Consume Projectile vẫn áp dụng. Bấm Apply / Restart sau khi chỉnh trong Play mode.

## Boost ngẫu nhiên

Boost ưu tiên ô thuộc **đội còn sống có ít grid nhất**, dựa trên quyền sở hữu hiện tại. Các đội bằng số ô được chọn ngẫu nhiên, sau đó chọn vị trí ngẫu nhiên trong ô của đội đó. Nếu lãnh thổ đội ít ô nhất bị chặn bởi cannon, mép arena hoặc boost khác, xét đội còn sống tiếp theo; nếu không có lãnh thổ hợp lệ của đội còn sống, quay về spawn ngẫu nhiên trong arena. Đội có 0 ô không có vùng riêng để spawn. Bỏ chọn `Boosts > Prefer Smaller Territories` để dùng spawn ngẫu nhiên toàn arena.

Khi còn đúng **2 đội**, khoảng spawn nhân **0,5**: ví dụ 15 giây thành 7,5 giây. Thời gian đang chờ cũng giảm một nửa ngay khi đội thứ ba bị loại, kể cả đang chờ lần spawn đầu tiên. Chỉnh hệ số ở `Boosts > Two Team Spawn Interval Multiplier`; đặt `1` để giữ nhịp cũ. Các giá trị Spawn Interval Min/Max gốc được giữ để dùng khi có 3–4 đội. Pause/Speed/Step vẫn áp dụng; Restart khôi phục lịch và vị trí theo seed. Apply / Restart sau khi chỉnh cấu hình trong Play mode.

Khi đạn bắn trúng, boost bay mượt về cannon đội bắn trong **1,2 giây mô phỏng**, thu nhỏ và mờ dần lúc tới nơi rồi biến mất. Sprite và nhãn bay cùng nhau. **Đội chỉ nhận boost khi vật phẩm tới cannon**; thời hạn tốc độ bắn/bi thêm bắt đầu lúc tới, còn x2 đạn áp dụng số đạn và hàng đợi Release còn lại lúc tới. Vật phẩm đang bay không thể được nhặt lần hai. Chuyến bay và hiệu lực dùng cùng thời gian mô phỏng, theo Pause/Speed/Step; Restart dọn toàn bộ vật phẩm đang bay. Cannon bị loại trước lúc tới không nhận thưởng; trận kết thúc hủy chuyến bay chưa hoàn tất. Trong `Boosts > Collection Flight`, chỉnh **Collection Flight Duration** để thay tốc độ bay, **Collection End Scale** để thay kích thước lúc tới, **Fade On Arrival** để bật/tắt mờ dần; tắt **Animate Collection** để vật phẩm biến mất và nhận boost ngay khi trúng. Apply / Restart sau khi chỉnh.

Chỉnh trong `DefaultSimulation > Boosts`, rồi bấm **Apply / Restart**. Boost xuất hiện trong arena sau 3 giây mô phỏng, tiếp theo ngẫu nhiên mỗi 5–10 giây; tối đa 3 vật phẩm, tự biến mất sau 20 giây nếu không được nhặt. Vị trí tránh cannon, mép bản đồ và vật phẩm khác. Seed quyết định vị trí, loại và nhịp xuất hiện.

- **SPD** (vàng): tốc độ bắn x2 trong 30 giây. Nhặt lại làm mới thời hạn, không cộng dồn hệ số. Áp dụng cả Shots Per Second và Frames Between Shots; khi boost làm tốc độ vượt một phát/frame, cannon có thể bắn nhiều phát trong cùng frame. Vẫn tuân theo giới hạn đạn active/spawn.
- **x2** (xanh lá): nhân đôi kho đạn và đạn còn chờ bắn sau Release; không nhân đạn đã bay. Kho tuân theo Max Stored Ammo; hàng chờ giới hạn ở số nguyên 64-bit, không tràn số.
- **+1** (xanh dương): thêm một bi Plinko hoạt động bình thường trong 30 giây. Mỗi bi thêm có thời hạn riêng; tối đa 5 bi thêm mỗi đội. Khi đạt giới hạn, nhặt tiếp làm mới thời hạn bi thêm cũ nhất. Hết hạn chỉ xóa bi đó, giữ nguyên bi còn lại.

Đội sở hữu viên đạn bắn trúng nhận boost, kể cả khi vật phẩm nằm trong lãnh thổ đội khác. Mỗi vật phẩm chỉ được nhặt một lần; đạn của đội đã bị loại không nhận boost. Consume Projectile bật mặc định: viên đạn nhặt vật phẩm biến mất. Tắt để đạn tiếp tục bay và xử lý grid như thường. Khi trận chuyển sang Settling/Finished, vật phẩm trên sân được dọn.

Mọi timer dùng thời gian mô phỏng: Pause dừng timer, Step tiến một tick, Speed thay đổi tốc độ đếm. Timer bi thêm vẫn chạy khi Plinko tạm dừng để Release. Restart xóa boost và bi tạm, khôi phục lượt chạy theo seed.

`Fire Rate / Double Ammo / Extra Marble` có Enabled, Spawn Weight, Sprite, Color và Label riêng. Bỏ chọn Enabled trong từng loại để tắt riêng loại đó, giữ nguyên Spawn Weight để bật lại. Enabled ở đầu Boosts tắt toàn bộ vật phẩm. Weight = 0 cũng ngừng spawn loại đó. Apply / Restart sau khi chỉnh. Sprite để None dùng hình tròn; kéo sprite mới vào từng loại để thay hình. Radius điều khiển va chạm; Visual Scale điều khiển kích thước hình. Có thể chỉnh thời gian spawn, số lượng, lifetime, khoảng tránh cannon/mép arena, hệ số/thời hạn tốc độ, thời hạn/số bi thêm, cỡ/màu chữ, Show Labels và Sorting Order. Diagnostics trong Inspector hiển thị số vật phẩm, số bi mỗi đội và thời gian speed boost còn lại.

Game simulation 2D cho Unity 6000.3.19f1, triển khai theo `C:/Users/tt/Downloads/Multiply or Release Docs.docx` và ảnh trong tài liệu.

## Chạy game

Mở `Assets/MultiplyOrRelease/Scenes/MultiplyOrRelease.unity` rồi bấm **Play**. Scene đã được đặt đầu danh sách Build Settings. Có thể mở lại bằng menu `Tools > Multiply or Release > Create or Open Simulation`; menu không ghi đè config hoặc scene đã có.

Chọn GameObject `Multiply or Release` để chỉnh config và điều khiển mô phỏng ngay trong Inspector, hoặc mở `Config/DefaultSimulation.asset`. Trong Edit mode, preview tự cập nhật khi chỉnh config. Trong Play mode, bấm **Apply / Restart** để áp dụng thay đổi cấu trúc/gameplay. Chỉnh asset config sẽ lưu vào project; các điều khiển Playback chỉ thay đổi lượt chạy hiện tại. Inspector dùng vùng cuộn có sẵn của Unity. Mặc định màn hình game chỉ hiển thị board, không có thanh thông tin hoặc nút điều khiển.

## Luật và lựa chọn mặc định

- 4 đội: Indonesia, Mexico, France, China; mỗi đội 5 bi Plinko và 1 đạn.
- Bi va chạm chốt và nhau trong mô phỏng vật lý 2D cục bộ. Vào ×2 sẽ nhân kho đạn; vào R sẽ đưa toàn bộ kho vào hàng đợi bắn, rồi đưa kho về 1. Bi quay lại phía trên; bi bị kẹt quá thời gian cấu hình được đưa lên lại và không được thưởng.
- Sau Release, kho đạn về 1 là lựa chọn triển khai vì tài liệu không quy định. Chỉnh `Cannon > Ammo After Release` để đổi.
- Đạn đi xuyên các ô cùng đội, đổi màu/flag ô đối phương và biến mất ngay sau khi chiếm ô. `Projectile > Despawn On Capture` bật mặc định và được ưu tiên hơn `Bounce On Capture`; tắt tùy chọn này nếu muốn dùng lại chế độ bật lại hoặc xuyên tiếp. Đạn bật ở biên arena.
- Cannon mặc định chết bởi một đạn đối phương. Có thể tắt `Destroy On Enemy Hit` và chỉnh `Teams > Hit Points` để dùng máu nhiều điểm.
- Đội chết dừng Plinko và hủy đạn chưa bắn. Đạn đã bay vẫn tồn tại và vẫn chiếm ô/phá cannon. Khi còn tối đa 1 đội, ngừng bắn mới và chờ đạn trên sân giải quyết hết; đội sống cuối cùng thắng. Đạn cuối có thể tạo kết quả hòa.
- Không có thời hạn trận mặc định. `Match Time Limit > 0` bật giới hạn, xếp hạng đội còn sống theo lãnh thổ; bằng điểm thì hòa.

## Chỉnh cấu hình

| Nhóm | Các thông số chính |
| --- | --- |
| Simulation | Auto start, seed, tốc độ, tick/giây, hạn mức tick/frame, thời hạn, thời gian chờ kết quả |
| Board | Số hàng/cột, kích thước, khe grid, màu nền, màu hoặc hình cờ, cách ánh xạ cờ, độ sáng, checker, đường biên, đội sở hữu từng quadrant |
| Plinko | Số bi, kích thước bảng, hàng/cột chốt, bán kính bi/chốt, gravity, restitution, damping, va chạm bi, spawn/kick, thời gian tái thả, ngưỡng chống kẹt, vị trí spawn/chốt, tỷ lệ ×2/R, mirror, màu và kích thước chữ/gate, trail |
| Cannon | Đạn đầu, đạn sau Release, trần kho, hệ số nhân, tốc độ bắn, vị trí góc, bán kính trúng đạn, kích thước marble/nòng/rim, màu khi bị loại |
| Projectile | Tốc độ, bán kính, lifetime, số đạn hoạt động tối đa, số spawn/tick, spread, biến mất sau chiếm ô, bật lại, phạm vi chiếm ô, chiều rộng/thời gian trail |
| Presentation | Sprite primitive, material sprite/trail, font, màu nền/frame/chữ, cỡ chữ, số rút gọn, tên đội/trail, camera auto-frame/focus/padding/offset |
| Teams [0..3] | Tên đội, sprite cannon/bi, texture cờ, màu grid/chữ/đạn/trail/nòng, tint sprite, góc ngắm, biên độ/tốc độ/chiều/phase xoay, kiểu xoay, HP |

Thứ tự `Teams`: trên trái, trên phải, dưới trái, dưới phải. Góc 0° hướng sang phải; 90° hướng lên. Sweep mặc định 120° dạng ping-pong quanh góc ngắm. `Continuous` lặp lại từ đầu khi đi hết biên độ, có thể đặt 360° để quay tròn.

`Territory Flag` dùng texture chữ nhật, cần bật **Read/Write** trong Import Settings. Có ba cách ánh xạ: co giãn một lá cờ theo khung lãnh thổ đang sở hữu (mặc định), cố định theo toàn arena, hoặc lặp theo kích thước quadrant ban đầu. Khi ô bị chiếm, ô dùng texture của đội mới. Cờ grid của Indonesia, Mexico, Pháp và Trung Quốc lấy từ `flag-icons` (MIT), chuyển SVG 4:3 thành PNG 1024×768; cờ Mexico có quốc huy từ bộ dữ liệu này, không dùng biểu tượng mẫu tự vẽ nữa. Công cụ tái tạo/thêm cờ ở `Tools/FlagAssets`; giấy phép ở `Art/FlagIcons-LICENSE.txt`. Cannon và bi vẫn dùng sprite có sẵn ở `Assets/MarbleFlag`, không sửa importer của các asset đó.

## Điều khiển trong Inspector

- `Celebration` port nguyên phần trình bày từ `C:/UnityGame/TerritoryBattle`: 3 → 2 → 1 → GO! với pop/outline/font Minecraft, winner card 700×600 với cờ tròn đội thắng, confetti/firework và sound `countdown.mp3` / `winner.mp3`. Giữ layout, màu, font, timing và thông số burst của `TerritoryGrid.unity`; file sound sao chép nguyên bản. Mã gốc được tách ra `SimulationCelebration` và `SimulationCelebrationFirework`, không đưa gameplay/skill/combat của TerritoryBattle vào.
- `Celebration > Enable Start Countdown`, `Enable Victory Card`, `Enable Victory Confetti`, `Enable Sounds` bật mặc định; mọi font, size, màu, thời gian, âm lượng/clip có thể chỉnh trong Inspector. Countdown dùng thời gian thực, không nhân theo Simulation Speed; simulation giữ nguyên ở Ready cho tới hết GO. Pause Intro dừng cả countdown và sound; Inspector Step bỏ qua intro để debug. Winner hiện theo kết quả cuối của simulation hiện tại (sau settling/result delay); sound winner phát một lần, confetti lặp cho tới Restart. Hòa hiện DRAW, không confetti/sound winner. Restart/Apply xóa UI, sound và particle của lượt trước. Khi winner card bật, HUD không hiện thêm result panel cũ.
- Ngoài Play mode, các nút `Preview 321 GO`, `Preview Winner`, `Clear Preview` trong Inspector cho xem bố cục ngay, không phát sound. Chỉnh config tự rebuild preview; preview không lưu vào scene. Card dùng cờ và tên đội hiện tại, không dùng roster USA/Brazil/... của project nguồn.
- `Plinko`: chỉnh vị trí từng loại chữ bằng `Ammo Text Offset` (số đạn), `Team Name Offset` (tên đội), `Multiply Text Offset` (×2), `Release Text Offset` (R), `Status Text Offset` (trạng thái). X dương sang phải, Y dương lên trên, đơn vị world; áp dụng cùng hướng trên cả 4 bảng, không đảo offset khi gate được mirror. `(0, 0)` giữ nguyên vị trí cũ. Ngoài Play mode preview tự cập nhật; trong Play mode bấm Apply / Restart. Offset không di chuyển gate hay thay đổi gameplay.
- `Plinko > Font`: kéo asset font `.ttf` / `.otf` vào để đổi font riêng cho toàn bộ chữ trong 4 bảng Plinko (số đạn, tên đội, ×2/R và trạng thái). Để `None` sẽ dùng `Presentation > Font` như cũ; không thay font HUD. Bấm Apply / Restart sau khi đổi, hoặc chỉnh ngoài Play mode để preview tự cập nhật.
- `Projectile > Visual Scale = 2` trong preset: đạn hiển thị to gấp đôi (cả chiều rộng/cao), giữ nguyên radius va chạm và tốc độ. Đặt `1` để về kích thước cũ. Kích thước hiển thị = `2 × Radius × Visual Scale`; bấm Apply / Restart sau khi chỉnh.
- `Projectile > Match Trail To Size` bật: trail tự rộng bằng đường kính hiển thị của đạn, kể cả khi đổi `Visual Scale`. Tắt để dùng `Trail Width` riêng. `Projectile > Taper Trail` và `Plinko > Taper Trail` đều tắt: trail giữ độ dày đều đến cuối, không nhọn đuôi; vẫn mờ dần và có cap bo tròn theo `Presentation > Trail Cap Vertices`. Bật lại `Taper Trail` nếu muốn thu nhọn như cũ. Trail bi Plinko vẫn dùng `Plinko > Trail Width`.
- `Projectile > Use Team Flag Sprite` bật mặc định: đạn dùng cờ tròn của đội tương ứng. Đổi asset tại `Teams > [đội] > Projectile Sprite`, chỉnh tint tại `Projectile Sprite Tint` (trắng giữ nguyên màu cờ). Nếu chưa gắn sprite, dùng `Cannon Sprite`; nếu cả hai trống hoặc tắt tùy chọn, dùng đạn tròn với `Projectile Color` như cũ. Kích thước vẫn theo `Projectile > Radius`, trail vẫn theo `Projectile Trail Color`; không thay đổi va chạm/capture. Sau khi chỉnh, bấm Apply / Restart.
- `Board > Flag Mapping > Fit Owned Territory` mặc định bật: mỗi đội dùng một lá cờ co giãn theo khung bao toàn bộ ô đang sở hữu; cờ chỉ hiện trên ô của đội đó, không lặp/clone khi chiếm thêm ô. Khung và UV tự cập nhật khi vùng mở rộng hoặc thu hẹp. Nếu lãnh thổ bị tách rời, các mảnh vẫn dùng chung một lá cờ, không nhân bản riêng. Có thể chuyển về `Repeat Starting Quadrant` để lặp như cũ, hoặc `Entire Arena` để cố định cờ theo toàn board. `Board > Style = Color` vẫn tắt hiển thị cờ. Trong Play mode, bấm Apply / Restart để đổi cấu hình.
- Khi đội bị loại, toàn bộ cannon (nòng, thân và viền tròn) được ẩn khỏi board; không để lại vòng tròn tối. Đạn đã bay vẫn tiếp tục theo luật cũ. Restart hiện lại cannon của cả 4 đội.
- `Plinko > Pause While Releasing` mặc định bật: Plinko của riêng đội có đạn trong hàng đợi Release đứng im (giữ vị trí, vận tốc, thời gian bi/spawn). Nòng vẫn quay và bắn; khi hàng đợi về 0, bi tiếp tục rơi, không chờ đạn đang bay biến mất. Viên bi chạm R vẫn tái thả theo luật cũ; các bi còn lại dừng ngay trong tick đó. Tắt tùy chọn để dùng hành vi Plinko chạy liên tục như trước.
- `Presentation > Show Release Countdown` mặc định bật: số lớn hiển thị đạn còn trong hàng đợi, giảm theo từng phát đã bắn rồi về `Ammo After Release` (mặc định 1), ví dụ Release 8: 8 → 7 → … → 1. Hàng đợi vẫn bắn đủ 8 viên, không mất viên cuối; nếu bị giới hạn spawn/active thì số giữ nguyên cho đến khi thực sự bắn. Tắt tùy chọn để hiện kho đạn đã reset như trước.
- `Cannon > Firing Mode`: chọn `Shots Per Second` (tốc độ theo giây mô phỏng, chịu ảnh hưởng Speed) hoặc `Frames Between Shots` (theo frame thực tế). Tốc độ quay nòng chỉnh riêng tại `Teams > Sweep Speed` (độ/giây). Chế độ frame: `Frames Between Shots = 1` bắn tối đa 1 viên/cannon/frame; `2` bắn cách một frame. Ở chế độ frame, FPS giảm sẽ giảm tốc độ bắn, không bắn bù thành chùm; Speed không thay đổi khoảng frame. Pause đóng băng bộ đếm, mỗi lần Step tính một frame; Restart đặt lại bộ đếm. Đạn chưa bắn vẫn giữ trong hàng đợi khi chạm giới hạn đạn. `Max Spawns Per Tick` là giới hạn chung cho cả 4 cannon, tính theo frame trong chế độ này. Trong Play mode, bấm Apply / Restart sau khi đổi chế độ hoặc khoảng bắn. Tái hiện cùng kết quả ở chế độ frame cần cả seed và cùng lịch frame/tick.
- `Board > Grid Line Thickness`: độ dày đường lưới theo tỷ lệ kích thước ô; 0 tắt đường lưới, 0.06 = 6%. `Border Thickness` vẫn chỉnh riêng đường biên lãnh thổ. Giá trị `Gap` cũ được giữ khi đổi tên.
- `Presentation > Ammo Text Sorting Order`: thứ tự render số đạn, mặc định 20 để nằm trên chốt/bi/trail Plinko. Giá trị lớn hơn render ở phía trên; không thay đổi vị trí hay độ trong suốt của chữ.
- Start / Pause / Resume; Step chạy đúng 1 tick rồi pause.
- Restart dùng lại seed đang chạy; New seed tăng seed và tạo trận mới.
- Speed chỉnh từ 0.1× đến 8× trong nhóm Playback.
- Grid appearance chọn Color/Flag; Show trails bật tắt trail.
- Inspector hiển thị trạng thái, thời gian, seed, tổng đạn, lượng đạn/hàng đợi chính xác và đội thắng.
- `Presentation > Camera Focus`: Simulation ôm toàn bộ grid và 4 bảng Plinko; Territory Grid chỉ lấy grid trung tâm. Camera Padding chỉnh khoảng trống; Camera Offset chỉnh tâm. Tắt Auto Frame Camera để chỉnh Camera trực tiếp bằng Inspector.
- `Plinko > Fit To 16 By 9` mặc định bật: Width/Height tự tính từ kích thước grid, Board Gap và Frame Thickness. Mép trên/dưới hai cột Plinko thẳng hàng với grid và có khung chung thành một hình chữ nhật 16:9. Tắt tùy chọn để chỉnh Width/Height thủ công. Camera Padding mặc định 0 để khung vừa đủ Game view 16:9; Game view tỷ lệ khác sẽ có khoảng trống đối xứng để giữ nguyên hình và không cắt board.

## Team presets / map positions

Mở `Tools > Multiply or Release > Team Manager` hoặc nút `Team Manager — Save / Load / Arrange` trên Controller/Config Inspector. Chọn Simulation Config, kéo 4 dòng (hoặc Move Up/Down) để đổi vị trí: Upper Left, Upper Right, Lower Left, Lower Right. Cannon, Plinko, cờ và màu đi cùng team; Aim Degrees tự xoay sang góc mới và giữ offset so với hướng vào tâm. `Match starting grid to slots` bật sẽ đưa Quadrant Owners về 0,1,2,3; tắt nếu muốn giữ mapping riêng.

`Save Selected Team As New Preset...` lưu mọi Team Settings vào asset dùng lại; chọn asset ở Team Preset rồi `Load Team Into Selected Position`. `Save Full Lineup As New Preset...` lưu cả 4 team, thứ tự và Quadrant Owners; `Load Full Lineup` khôi phục đúng đội hình. Preset là bản sao: chỉnh config không đổi preset. Save luôn tạo asset mới, không ghi đè preset cũ. Có Undo/Redo; preview cập nhật trong Edit Mode. Trong Play Mode dùng `Apply / Restart Scene` để áp dụng. `Save Config To Disk` lưu thay đổi config. Cửa sổ có scrollbar ở cả hai chiều; mở Selected Team Settings để chỉnh chi tiết.

## MarbleFlag team library

Thư viện đầy đủ cho `Assets/MarbleFlag` nằm trong `TeamPresets`: mỗi cờ có một Team Preset với sprite cho cannon/bi/đạn và texture chữ nhật readable cho grid. `Art/TeamFlags/MarbleFlagCatalog.json` ghi nguồn và ánh xạ. Sinh lại bằng `npm run generate:teams` trong `Tools/FlagAssets`, refresh Unity rồi menu `Tools > Multiply or Release > Generate Missing MarbleFlag Team Presets`; menu chỉ thêm preset thiếu, không ghi đè preset đã chỉnh. Ba cờ vùng Arab League/Saba/Sint Eustatius dùng artwork gốc trên nền chữ nhật kín thay vì giả nhận là cờ chữ nhật chuẩn của flag-icons.

## Cannon firing pop

`DefaultSimulation > Cannon > Flag Firing Pop`: Enable Fire Pop bật/tắt scale pop của lá cờ cannon khi đạn thực sự được bắn ra; Fire Pop Scale = 1.2 phóng lớn 20%, Fire Pop Duration là thời gian về kích thước gốc (giây ở Speed 1). Pop chỉ thay đổi hình ảnh, không đổi nòng, vị trí cannon, collider hay nhịp bắn. Khi bắn nhanh, các phát trong một pop dùng chung nhịp hiện tại để không cộng dồn hoặc giữ cờ mãi ở kích thước lớn. Pause/Speed/Step áp dụng cho animation; Apply / Restart sau khi đổi config.

## Grid impact VFX

`DefaultSimulation > Grid Impact` bật/tắt VFX khi đạn chiếm grid. Mỗi viên đạn chạm chỉ tạo đúng một hạt tròn, kể cả Capture Radius chiếm nhiều ô. Hạt không có trail; mặc định lấy Projectile Color của đội bắn. Tắt Use Team Color để chọn một màu chung. Duration, Size In Cells, Spread In Cells, Opacity và Halo Opacity chỉnh hình dáng hiệu ứng; kích thước tự theo độ phân giải grid. Spread In Cells = 0 giữ hạt tại điểm trúng. Max Particles giới hạn pool, tái dùng hạt cũ khi đông đạn. Pause/Speed/Step điều khiển cả hiệu ứng. Apply / Restart sau khi đổi config.

## Hạn mức và kiến trúc

Kho đạn dùng số nguyên 64-bit. `Max Stored Ammo` mặc định 1,073,741,824; đạt trần sẽ hiển thị `MAX` thay vì tràn số. Hàng đợi lưu nguyên số đạn đã Release. Hạn mức active/spawn chỉ trì hoãn bắn; không bỏ phần còn lại. Khi tổng hàng đợi vượt phạm vi 64-bit, giữ kho hiện tại và báo `QUEUE FULL`. Đạn chưa bắn chỉ bị hủy khi đội bị loại hoặc trận kết thúc.

Model chạy bước cố định với RNG riêng và không đổi `Time.timeScale`/Physics2D toàn project. Seed cố định tái hiện cùng kết quả với cùng config. Renderer có pool đạn/trail và một mesh cho grid; preview/atlas/mesh được tạo lại khi mở scene và không ghi dữ liệu tạm vào scene. Tốc độ quá cao trên máy yếu có thể chậm hơn yêu cầu và Inspector báo `CPU LIMIT`.

- `Scripts/SimulationConfig.cs`: cấu hình.
- `Scripts/SimulationModel.cs`: luật, Plinko, grid, cannon, đạn, kết quả.
- `Scripts/SimulationController.cs`: vòng lặp, preview, điều khiển.
- `Scripts/SimulationView.cs`: mesh grid, sprites, trail, pool.
- `Scripts/SimulationHud.cs`: UI và Input System.
- `Editor/SimulationSceneBuilder.cs`: dựng scene/assets mẫu và Inspector.
- `Tests/Editor`: test luật, replay seed, gate vật lý, sweep, giới hạn, hit cannon và trận hoàn chỉnh.

Chạy test ở `Window > General > Test Runner`: các test EditMode trong assembly `MultiplyOrRelease.Tests` bao gồm đạn biến mất sau chiếm ô và đi xuyên ô cùng đội; test PlayMode trong `MultiplyOrRelease.PlayModeTests` kiểm tra màn hình không có UI, camera ôm board và các điều khiển pause, step, grid, trail, tốc độ trong Inspector. Trong mọi lần làm việc bằng AI, luôn kiểm tra Unity MCP và project đang được chọn theo `AGENTS.md` ở gốc project.
