using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WinFormsApp1
{
    // ComboBox에 한국어 표시, 영문 값 저장을 위한 클래스
    public class AttributeComboBoxItem
    {
        public string DisplayText { get; set; }  // 한국어 표시 텍스트
        public string? EnglishValue { get; set; }   // 영문 저장 값 (null 가능)
        
        public AttributeComboBoxItem(string displayText, string? englishValue)
        {
            DisplayText = displayText;
            EnglishValue = englishValue;
        }
        
        public override string ToString()
        {
            return DisplayText;
        }
    }

    public partial class PersonAttributesForm : Form
    {
        private Dictionary<string, ComboBox> attributeControls = new Dictionary<string, ComboBox>();
        private int personId;
        private int waypointEntryFrame;
        private int currentFrameIndex;
        private Func<int, int, string, object> getAttributeFunc;
        private Action<int, int, string, object> setAttributeFunc;
        
        // 노란색 표시 속성 목록 (Waypoint-scoped)
        private static readonly HashSet<string> waypointScopedAttributes = new HashSet<string>
        {
            "Occlusion",      // View 탭의 노란색 표시 속성
            "BodyView",       // View 탭의 노란색 표시 속성
            "ActionType"      // Action 탭의 노란색 표시 속성
        };

        public PersonAttributesForm(int personId, int waypointEntryFrame, int currentFrameIndex,
            Func<int, int, string, object> getAttributeFunc,
            Action<int, int, string, object> setAttributeFunc)
        {
            this.personId = personId;
            this.waypointEntryFrame = waypointEntryFrame;
            this.currentFrameIndex = currentFrameIndex;
            this.getAttributeFunc = getAttributeFunc;
            this.setAttributeFunc = setAttributeFunc;
            
            InitializeComponent();
            LoadCurrentAttributes();
        }

        private void InitializeComponent()
        {
            this.Text = $"Person {personId:D2} 속성 편집";
            this.Size = new Size(800, 650);  // 높이 증가
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 정보 레이블
            Label infoLabel = new Label
            {
                Text = $"Person ID: {personId:D2} | Waypoint Entry Frame: {waypointEntryFrame} | Current Frame: {currentFrameIndex}",
                Location = new Point(0, 0),
                Size = new Size(800, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 0, 0),
                BackColor = SystemColors.Control
            };
            this.Controls.Add(infoLabel);

            // TabControl - 상단 정보 레이블과 하단 버튼 패널 사이 공간 사용
            TabControl tabControl = new TabControl
            {
                Location = new Point(0, 30),
                Size = new Size(784, 530),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                TabIndex = 0
            };

            // View + Action 탭 (보임/가림/행동)
            TabPage viewActionTab = CreateTabPage("보임/가림/행동", new[]
            {
                ("Occlusion", new[] { "Person-Multi", "Person-FullyVisible", "Person-PartiallyVisible", "OccludedPart-Head", "OccludedPart-UpperBody", "OccludedPart-LowerBody", "OccludedPart-Feet", "Occluded-byPerson" }),
                ("BodyView", new[] { "BodyView-Back", "BodyView-Front", "BodyView-Side" }),
                ("ActionType", new[] { "Standing", "Walking", "Running", "Riding", "Sitting", "Pulling" })
            });
            tabControl.TabPages.Add(viewActionTab);

            // Biometric 탭 (생체 정보)
            TabPage biometricTab = CreateTabPage("생체 정보", new[]
            {
                ("Age", new[] { "Age-Minor", "Age-Adult", "Age-Old" }),
                ("Gender", new[] { "Gender-Female", "Gender-Male" }),
                ("Height", new[] { "Height-Short", "Height-Average", "Height-Tall" }),
                ("Weight", new[] { "Weight-Underweight", "Weight-Average", "Weight-Overweight" }),
                ("BodyPosture", new[] { "BodyPosture-Stooped" }),
                ("Face", new[] { "Face-Recognizable" })
            });
            tabControl.TabPages.Add(biometricTab);

            // Head/Hair 탭 (머리/헤어)
            TabPage headHairTab = CreateTabPage("머리/헤어", new[]
            {
                ("HairLength", new[] { "HairLength-Bald", "HairLength-Short", "HairLength-Medium", "HairLength-Long" }),
                ("HairStyle", new[] { "HairStyle-Ponytail" }),
                ("HairColor", new[] { "HairColor-Dark", "HairColor-Light", "HairColor-Colored" })
            });
            tabControl.TabPages.Add(headHairTab);

            // UpperCloth 탭 (상의)
            TabPage upperClothTab = CreateTabPage("상의", new[]
            {
                ("UpperClothType", new[] { "Upper-Type-Tshirt", "Upper-Type-Shirt", "Upper-Type-Sweater", "Upper-Type-Jacket", "Upper-Type-Blazer", "Upper-Type-LongCoat", "Upper-Type-Dress" }),
                ("UpperClothSleeve", new[] { "Upper-Sleeve-Sleeveless", "Upper-Sleeve-Short", "Upper-Sleeve-Long" }),
                ("UpperClothPattern", new[] { "Upper-Pattern-Solid", "Upper-Pattern-Logo", "Upper-Pattern-Plaid", "Upper-Pattern-Stripe", "Upper-Pattern-Splice", "Upper-Pattern-Graphics" }),
                ("UpperClothColor", new[] { "Upper-Color-Black", "Upper-Color-Blue", "Upper-Color-Brown", "Upper-Color-Green", "Upper-Color-Grey", "Upper-Color-Orange", "Upper-Color-Pink", "Upper-Color-Purple", "Upper-Color-Red", "Upper-Color-White", "Upper-Color-Yellow" })
            });
            tabControl.TabPages.Add(upperClothTab);

            // LowerCloth 탭 (하의)
            TabPage lowerClothTab = CreateTabPage("하의", new[]
            {
                ("LowerClothType", new[] { "Lower-Type-Pants", "Lower-Type-Skirt" }),
                ("LowerClothLegwear", new[] { "Lower-Legwear-Tights" }),
                ("LowerClothLength", new[] { "Lower-Length-Short", "Lower-Length-MidCalf", "Lower-Length-Full" }),
                ("LowerClothPattern", new[] { "Lower-Pattern-Solid", "Lower-Pattern-Plaid", "Lower-Pattern-Stripe", "Lower-Pattern-Graphics" }),
                ("LowerClothColor", new[] { "Lower-Color-Black", "Lower-Color-Blue", "Lower-Color-Brown", "Lower-Color-Green", "Lower-Color-Grey", "Lower-Color-Pink", "Lower-Color-Purple", "Lower-Color-Red", "Lower-Color-White", "Lower-Color-Yellow" }),
                ("LowerClothMaterial", new[] { "Lower-Material-Denim" })
            });
            tabControl.TabPages.Add(lowerClothTab);

            // Footwear 탭 (신발)
            TabPage footwearTab = CreateTabPage("신발", new[]
            {
                ("FootwearType", new[] { "Footwear-Type-Boots", "Footwear-Type-Flats", "Footwear-Type-Formal", "Footwear-Type-Sandals", "Footwear-Type-Sneakers" }),
                ("FootwearColor", new[] { "Footwear-Color-Black", "Footwear-Color-Brown", "Footwear-Color-White" })
            });
            tabControl.TabPages.Add(footwearTab);

            // Accessory 탭 (악세서리)
            TabPage accessoryTab = CreateTabPage("악세서리", new[]
            {
                ("HeadwearType", new[] { "Headwear-Hat", "Headwear-Halmet", "Headwear-Other" }),
                ("FacewearType", new[] { "Facewear-Glasses", "Facewear-Sunglasses", "Facewear-Mask" }),
                ("BagType", new[] { "Bag-Backpack", "Bag-Handbag", "Bag-ShoulderBag", "Bag-Suitcase" }),
                ("CarringItemType", new[] { "Carrying-Phone", "Carrying-Umbrella", "Carrying-Drink", "Carrying-Box", "Carrying-Stick", "HandsOccupied" })
            });
            tabControl.TabPages.Add(accessoryTab);


            this.Controls.Add(tabControl);

            // 하단 버튼 패널 - 고정된 하단 영역
            Panel buttonPanel = new Panel
            {
                Location = new Point(0, 560),
                Size = new Size(800, 50),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = SystemColors.Control
            };

            Button btnOK = new Button
            {
                Text = "확인",
                DialogResult = DialogResult.OK,
                Location = new Point(600, 10),
                Size = new Size(80, 30),
                TabIndex = 1000
            };
            btnOK.Click += BtnOK_Click;

            Button btnCancel = new Button
            {
                Text = "취소",
                DialogResult = DialogResult.Cancel,
                Location = new Point(690, 10),
                Size = new Size(80, 30),
                TabIndex = 1001
            };

            buttonPanel.Controls.Add(btnOK);
            buttonPanel.Controls.Add(btnCancel);
            this.Controls.Add(buttonPanel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        // 속성 값 한국어 매핑 (JSON 저장은 영문으로 유지)
        private static readonly Dictionary<string, string> AttributeValueKoreanMap = new Dictionary<string, string>
        {
            // View
            { "Person-Multi", "한명 이상 포함" },
            { "Person-FullyVisible", "대상 인물 전신 전체가 보임" },
            { "Person-PartiallyVisible", "전신 중 일부 가려짐" },
            { "OccludedPart-Head", "머리가 안보임" },
            { "OccludedPart-UpperBody", "상반신이 안보임" },
            { "OccludedPart-LowerBody", "하반신이 안보임" },
            { "OccludedPart-Feet", "양발이 다 안보임" },
            { "Occluded-byPerson", "대상 인물이 타인에 의해 가려짐" },
            { "BodyView-Back", "후면" },
            { "BodyView-Front", "전면" },
            { "BodyView-Side", "측면" },
            
            // Biometric
            { "Age-Minor", "미성년자(어린이, 초중고)" },
            { "Age-Adult", "성인" },
            { "Age-Old", "노인" },
            { "Gender-Female", "여자" },
            { "Gender-Male", "남자" },
            { "Height-Short", "키작음(<145cm, 어린이, 초등학생정도)" },
            { "Height-Average", "키보통" },
            { "Height-Tall", "키큼(>180cm)" },
            { "Weight-Underweight", "체격_마름" },
            { "Weight-Average", "체격_보통" },
            { "Weight-Overweight", "체격_과체중(curvy한 체형)" },
            { "BodyPosture-Stooped", "등이 굽은 체형" },
            { "Face-Recognizable", "안면 인식이 가능한 정도" },
            
            // Head/Hair
            { "HairLength-Bald", "대머리(부분 대머리 포함)" },
            { "HairLength-Short", "짧은 머리" },
            { "HairLength-Medium", "단발 머리(어깨선 정도 길이)" },
            { "HairLength-Long", "긴 머리(어깨선 이하로)" },
            { "HairStyle-Ponytail", "묶은 머리형태" },
            { "HairColor-Dark", "검은색, 갈색" },
            { "HairColor-Light", "흰머리, 회색" },
            { "HairColor-Colored", "금발, 빨강" },
            
            // UpperCloth
            { "Upper-Type-Tshirt", "긴팔/반팔 티셔츠, 캐주얼 폴로티, 민소매티" },
            { "Upper-Type-Shirt", "셔츠(카라, 버튼다운), 블라우스" },
            { "Upper-Type-Sweater", "니트 스웨터, 가디건, 맨투맨 스웻셔츠, 후드 스웻" },
            { "Upper-Type-Jacket", "캐주얼 겉옷(잠바, 트렌치코트, 봄버, 가죽자켓 등)" },
            { "Upper-Type-Blazer", "양복자켓, 콤비자켓 등" },
            { "Upper-Type-LongCoat", "허벅지 중간보다 긴 길이의 겉옷" },
            { "Upper-Type-Dress", "원피스" },
            { "Upper-Sleeve-Sleeveless", "민소매" },
            { "Upper-Sleeve-Short", "반팔 소매" },
            { "Upper-Sleeve-Long", "긴 소매" },
            { "Upper-Pattern-Solid", "무늬 없는 단색" },
            { "Upper-Pattern-Logo", "로고(브랜드 로고, 글자로고, 중앙/단일 그래픽, 캐릭터 등)" },
            { "Upper-Pattern-Plaid", "체크 무늬" },
            { "Upper-Pattern-Stripe", "줄 무늬(가로, 세로, 사선)" },
            { "Upper-Pattern-Splice", "배색 무늬(color-block)" },
            { "Upper-Pattern-Graphics", "상의전체 반복 패턴(폴카닷, 꽃무늬, 기하학 반복 무늬)" },
            { "Upper-Color-Black", "검정" },
            { "Upper-Color-Blue", "파랑" },
            { "Upper-Color-Brown", "갈색" },
            { "Upper-Color-Green", "초록" },
            { "Upper-Color-Grey", "회색" },
            { "Upper-Color-Orange", "주황" },
            { "Upper-Color-Pink", "분홍" },
            { "Upper-Color-Purple", "보라" },
            { "Upper-Color-Red", "빨강" },
            { "Upper-Color-White", "흰색" },
            { "Upper-Color-Yellow", "노랑" },
            
            // LowerCloth
            { "Lower-Type-Pants", "하의유형_바지" },
            { "Lower-Type-Skirt", "하의유형_치마" },
            { "Lower-Legwear-Tights", "하의_타이즈/레깅스 착용" },
            { "Lower-Length-Short", "하의길이_무릅 기준" },
            { "Lower-Length-MidCalf", "하의길이_정강이 중간 기준" },
            { "Lower-Length-Full", "하의길이_발목 기준" },
            { "Lower-Pattern-Solid", "하의무늬_단색(무늬 없음)" },
            { "Lower-Pattern-Plaid", "하의무늬_체크" },
            { "Lower-Pattern-Stripe", "하의무늬_줄무늬(가로, 세로, 사선 줄이 한 개 이상)" },
            { "Lower-Pattern-Graphics", "하의무늬_하의전체 반복(점, 꽃무늬, 군복위장무늬 등)" },
            { "Lower-Color-Black", "검정" },
            { "Lower-Color-Blue", "파랑" },
            { "Lower-Color-Brown", "갈색" },
            { "Lower-Color-Green", "초록" },
            { "Lower-Color-Grey", "회색" },
            { "Lower-Color-Pink", "분홍" },
            { "Lower-Color-Purple", "보라" },
            { "Lower-Color-Red", "빨강" },
            { "Lower-Color-White", "흰색" },
            { "Lower-Color-Yellow", "노랑" },
            { "Lower-Material-Denim", "데님소재(청바지, 청치마)" },
            
            // Footwear
            { "Footwear-Type-Boots", "부츠(발목 위~무릅까지 커버)" },
            { "Footwear-Type-Flats", "발등이 노출되는 구조의 신발" },
            { "Footwear-Type-Formal", "구두(가죽소재), 신사화, 여성용힐" },
            { "Footwear-Type-Sandals", "발가락, 뒷꿈치가 노출되는 구조의 실발(슬리퍼 포함)" },
            { "Footwear-Type-Sneakers", "운동화" },
            { "Footwear-Color-Black", "검정" },
            { "Footwear-Color-Brown", "갈색류" },
            { "Footwear-Color-White", "흰색" },
            
            // Accessory
            { "Headwear-Hat", "모자" },
            { "Headwear-Halmet", "헬맷(딱딱한 소재, 오토바이/자전거 헬맷)" },
            { "Headwear-Other", "다른 형태의 머리 전체를 커버하는 악세서리" },
            { "Facewear-Glasses", "안경착용" },
            { "Facewear-Sunglasses", "썬글라스착용" },
            { "Facewear-Mask", "마스크 착용" },
            { "Bag-Backpack", "백팩" },
            { "Bag-Handbag", "leather, plastic, paper bags worn by hands" },
            { "Bag-ShoulderBag", "한쪽 어깨에 걸치는 형태의 가방(메신저, 크로스백 등)" },
            { "Bag-Suitcase", "바퀴 달린 형태의 가방(여행용 캐리어, 쇼핑카트)" },
            { "Carrying-Phone", "휴대폰 소지" },
            { "Carrying-Umbrella", "우산(펼친 우산, 접은 우산) 소지" },
            { "Carrying-Drink", "음료수 컵, 생수병 등 소지" },
            { "Carrying-Box", "박스 소지" },
            { "Carrying-Stick", "지팡이, 등산스틱, 목발 등 소지" },
            { "HandsOccupied", "한손 또는 양손에 물건(가방, 소지품) 소지(빈손이 아님)" },
            
            // Action
            { "Standing", "서있음" },
            { "Walking", "걷고 있음" },
            { "Running", "뛰고 있음" },
            { "Riding", "타고 있음(자전거, 오토바이, 퀵보드 등)" },
            { "Sitting", "앉아 있음(모빌리티 제외한 의자, 고정형 구조물에)" },
            { "Pulling", "끌고 있음(유모차, 자전거, 카트, 캐리어 등)" }
        };

        // 영문 속성 값을 한국어로 변환
        public static string GetAttributeValueKorean(string englishValue)
        {
            if (string.IsNullOrEmpty(englishValue))
                return englishValue;
            
            return AttributeValueKoreanMap.TryGetValue(englishValue, out string korean) ? korean : englishValue;
        }

        // 속성 이름을 한국어로 변환
        private string GetKoreanAttributeName(string attributeName)
        {
            var mapping = new Dictionary<string, string>
            {
                // View 탭
                { "Occlusion", "가림 정도" },
                { "BodyView", "신체 방향" },
                // Biometric 탭
                { "Age", "연령대" },
                { "Gender", "성별" },
                { "Height", "키" },
                { "Weight", "체중" },
                { "BodyPosture", "자세" },
                { "Face", "얼굴" },
                // Head/Hair 탭
                { "HairLength", "머리 길이" },
                { "HairStyle", "헤어 스타일" },
                { "HairColor", "머리 색상" },
                // UpperCloth 탭
                { "UpperClothType", "상의 종류" },
                { "UpperClothSleeve", "소매 길이" },
                { "UpperClothPattern", "상의 무늬" },
                { "UpperClothColor", "상의 색상" },
                // LowerCloth 탭
                { "LowerClothType", "하의 종류" },
                { "LowerClothLegwear", "레그웨어" },
                { "LowerClothLength", "하의 길이" },
                { "LowerClothPattern", "하의 무늬" },
                { "LowerClothColor", "하의 색상" },
                { "LowerClothMaterial", "하의 소재" },
                // Footwear 탭
                { "FootwearType", "신발 종류" },
                { "FootwearColor", "신발 색상" },
                // Accessory 탭
                { "HeadwearType", "모자 종류" },
                { "FacewearType", "안면 악세서리" },
                { "BagType", "가방 종류" },
                { "CarringItemType", "휴대 물품" },
                // Action 탭
                { "ActionType", "행동" }
            };

            return mapping.ContainsKey(attributeName) ? mapping[attributeName] : attributeName;
        }

        private TabPage CreateTabPage(string tabName, (string name, string[] values)[] attributes)
        {
            TabPage tabPage = new TabPage(tabName);
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            int yPos = 10;
            foreach (var (attrName, values) in attributes)
            {
                bool isWaypointScoped = waypointScopedAttributes.Contains(attrName);
                string koreanName = GetKoreanAttributeName(attrName);
                
                Label label = new Label
                {
                    Text = $"{koreanName} ({attrName})",
                    Location = new Point(10, yPos),
                    Size = new Size(200, 20),
                    ForeColor = isWaypointScoped ? Color.Orange : Color.Black
                };

                ComboBox comboBox = new ComboBox
                {
                    Location = new Point(220, yPos - 2),
                    Size = new Size(500, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                
                comboBox.Items.Add(new AttributeComboBoxItem("(없음)", null)); // null 값 표시
                foreach (string englishValue in values)
                {
                    string koreanText = GetAttributeValueKorean(englishValue);
                    comboBox.Items.Add(new AttributeComboBoxItem(koreanText, englishValue));
                }
                
                attributeControls[attrName] = comboBox;

                panel.Controls.Add(label);
                panel.Controls.Add(comboBox);
                yPos += 35;
            }

            tabPage.Controls.Add(panel);
            return tabPage;
        }

        private void LoadCurrentAttributes()
        {
            foreach (var kvp in attributeControls)
            {
                string attrName = kvp.Key;
                ComboBox comboBox = kvp.Value;
                
                object value = getAttributeFunc(personId, currentFrameIndex, attrName);
                
                if (value == null)
                {
                    comboBox.SelectedIndex = 0; // "(없음)"
                }
                else
                {
                    string valueStr = value.ToString();
                    // 영문 값으로 항목 찾기
                    bool found = false;
                    for (int i = 1; i < comboBox.Items.Count; i++)
                    {
                        if (comboBox.Items[i] is AttributeComboBoxItem item)
                        {
                            if (item.EnglishValue == valueStr)
                            {
                                comboBox.SelectedIndex = i;
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        comboBox.SelectedIndex = 0;
                    }
                }
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            foreach (var kvp in attributeControls)
            {
                string attrName = kvp.Key;
                ComboBox comboBox = kvp.Value;
                
                // 현재 저장된 값 가져오기
                object currentValue = getAttributeFunc(personId, currentFrameIndex, attrName);
                
                object newValue = null;
                if (comboBox.SelectedIndex > 0 && comboBox.Items[comboBox.SelectedIndex] is AttributeComboBoxItem item)
                {
                    newValue = item.EnglishValue; // 영문 값 저장
                }
                
                // 값이 변경된 경우에만 저장
                string currentValueStr = currentValue?.ToString() ?? "";
                string newValueStr = newValue?.ToString() ?? "";
                
                if (currentValueStr != newValueStr)
                {
                    setAttributeFunc(personId, waypointEntryFrame, attrName, newValue);
                }
            }
        }
    }
}