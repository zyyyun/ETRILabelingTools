using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WinFormsApp1
{
    public partial class PersonAttributesForm : Form
    {
        private Dictionary<string, ComboBox> attributeControls = new Dictionary<string, ComboBox>();
        private int personId;
        private int waypointEntryFrame;
        private int currentFrameIndex;
        private Func<int, int, string, object> getAttributeFunc;
        private Action<int, int, string, object> setAttributeFunc;
        private bool isWaypointScoped;
        
        // 노란색 표시 속성 목록 (Waypoint-scoped)
        private static readonly HashSet<string> waypointScopedAttributes = new HashSet<string>
        {
            // 이미지에서 노란색으로 표시된 속성들 (추후 추가)
            // 예: "Occlusion", "BodyView" 등
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
            this.isWaypointScoped = PersonAttributeStore.IsWaypointScoped(""); // 기본값
            
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

            // View 탭
            TabPage viewTab = CreateTabPage("View", new[]
            {
                ("Occlusion", new[] { "Person-Multi", "Person-FullyVisible", "Person-PartiallyVisible", "OccludedPart-Head", "OccludedPart-UpperBody", "OccludedPart-LowerBody", "OccludedPart-Feet", "Occluded-byPerson" }),
                ("BodyView", new[] { "BodyView-Back", "BodyView-Front", "BodyView-Side" })
            });
            tabControl.TabPages.Add(viewTab);

            // Biometric 탭
            TabPage biometricTab = CreateTabPage("Biometric", new[]
            {
                ("Age", new[] { "Age-Minor", "Age-Adult", "Age-Old" }),
                ("Gender", new[] { "Gender-Female", "Gender-Male" }),
                ("Height", new[] { "Height-Short", "Height-Average", "Height-Tall" }),
                ("Weight", new[] { "Weight-Underweight", "Weight-Average", "Weight-Overweight" }),
                ("BodyPosture", new[] { "BodyPosture-Stooped" }),
                ("Face", new[] { "Face-Recognizable" })
            });
            tabControl.TabPages.Add(biometricTab);

            // Head/Hair 탭
            TabPage headHairTab = CreateTabPage("Head/Hair", new[]
            {
                ("HairLength", new[] { "HairLength-Bald", "HairLength-Short", "HairLength-Medium", "HairLength-Long" }),
                ("HairStyle", new[] { "HairStyle-Ponytail" }),
                ("HairColor", new[] { "HairColor-Dark", "HairColor-Light", "HairColor-Colored" })
            });
            tabControl.TabPages.Add(headHairTab);

            // UpperCloth 탭
            TabPage upperClothTab = CreateTabPage("UpperCloth", new[]
            {
                ("UpperClothType", new[] { "Upper-Type-Tshirt", "Upper-Type-Shirt", "Upper-Type-Sweater", "Upper-Type-Jacket", "Upper-Type-Blazer", "Upper-Type-LongCoat", "Upper-Type-Dress" }),
                ("UpperClothSleeve", new[] { "Upper-Sleeve-Sleeveless", "Upper-Sleeve-Short", "Upper-Sleeve-Long" }),
                ("UpperClothPattern", new[] { "Upper-Pattern-Solid", "Upper-Pattern-Logo", "Upper-Pattern-Plaid", "Upper-Pattern-Stripe", "Upper-Pattern-Splice", "Upper-Pattern-Graphics" }),
                ("UpperClothColor", new[] { "Upper-Color-Black", "Upper-Color-Blue", "Upper-Color-Brown", "Upper-Color-Green", "Upper-Color-Grey", "Upper-Color-Orange", "Upper-Color-Pink", "Upper-Color-Purple", "Upper-Color-Red", "Upper-Color-White", "Upper-Color-Yellow" })
            });
            tabControl.TabPages.Add(upperClothTab);

            // LowerCloth 탭
            TabPage lowerClothTab = CreateTabPage("LowerCloth", new[]
            {
                ("LowerClothType", new[] { "Lower-Type-Pants", "Lower-Type-Skirt" }),
                ("LowerClothLegwear", new[] { "Lower-Legwear-Tights" }),
                ("LowerClothLength", new[] { "Lower-Length-Short", "Lower-Length-MidCalf", "Lower-Length-Full" }),
                ("LowerClothPattern", new[] { "Lower-Pattern-Solid", "Lower-Pattern-Plaid", "Lower-Pattern-Stripe", "Lower-Pattern-Graphics" }),
                ("LowerClothColor", new[] { "Lower-Color-Black", "Lower-Color-Blue", "Lower-Color-Brown", "Lower-Color-Green", "Lower-Color-Grey", "Lower-Color-Pink", "Lower-Color-Purple", "Lower-Color-Red", "Lower-Color-White", "Lower-Color-Yellow" }),
                ("LowerClothMaterial", new[] { "Lower-Material-Denim" })
            });
            tabControl.TabPages.Add(lowerClothTab);

            // Footwear 탭
            TabPage footwearTab = CreateTabPage("Footwear", new[]
            {
                ("FootwearType", new[] { "Footwear-Type-Boots", "Footwear-Type-Flats", "Footwear-Type-Formal", "Footwear-Type-Sandals", "Footwear-Type-Sneakers" }),
                ("FootwearColor", new[] { "Footwear-Color-Black", "Footwear-Color-Brown", "Footwear-Color-White" })
            });
            tabControl.TabPages.Add(footwearTab);

            // Accessory 탭
            TabPage accessoryTab = CreateTabPage("Accessory", new[]
            {
                ("HeadwearType", new[] { "Headwear-Hat", "Headwear-Halmet", "Headwear-Other" }),
                ("FacewearType", new[] { "Facewear-Glasses", "Facewear-Sunglasses", "Facewear-Mask" }),
                ("BagType", new[] { "Bag-Backpack", "Bag-Handbag", "Bag-ShoulderBag", "Bag-Suitcase" }),
                ("CarringItemType", new[] { "Carrying-Phone", "Carrying-Umbrella", "Carrying-Drink", "Carrying-Box", "Carrying-Stick", "HandsOccupied" })
            });
            tabControl.TabPages.Add(accessoryTab);

            // Action 탭
            TabPage actionTab = CreateTabPage("Action", new[]
            {
                ("ActionType", new[] { "Standing", "Walking", "Running", "Riding", "Sitting", "Pulling" })
            });
            tabControl.TabPages.Add(actionTab);

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
                bool isWaypointScoped = PersonAttributeStore.IsWaypointScoped(attrName);
                
                Label label = new Label
                {
                    Text = attrName + (isWaypointScoped ? " (Waypoint-scoped)" : " (Global)"),
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
                
                comboBox.Items.Add("(없음)"); // null 값 표시
                comboBox.Items.AddRange(values);
                
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
                    int index = comboBox.Items.IndexOf(valueStr);
                    if (index >= 0)
                    {
                        comboBox.SelectedIndex = index;
                    }
                    else
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
                
                object value = null;
                if (comboBox.SelectedIndex > 0)
                {
                    value = comboBox.Items[comboBox.SelectedIndex].ToString();
                }
                
                setAttributeFunc(personId, waypointEntryFrame, attrName, value);
            }
        }
    }
}