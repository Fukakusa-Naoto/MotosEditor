using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace MapEditor
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		// 選択ID
		enum SelectID
		{
			Floor, PlayerParts, Enemy
		}

		// オブジェクトID
		enum ObjectID : int
		{
			None, Player, Power, Jump, Enemy
		}

		// プロパティ
		int chipSize = 32;  // チップのサイズ
		int stageW = 10;    // ステージの横のチップ数
		int stageH = 10;    // ステージの縦のチップ数
		int[] stageTable;   // ステージデータの配列
		ObjectID[] objectTable; // 配置オブジェクトデータの配列
		int selectChip = 0; // 選択されているチップ番号
		SelectID selectID = SelectID.Floor; // 選択されているチップの種類


		public MainWindow()
		{
			InitializeComponent();
		}


		private System.Drawing.Point PositionToChipPosition(int x, int y)
		{
			return new System.Drawing.Point(x / chipSize * chipSize, y / chipSize * chipSize);
		}


		private void floorImage_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// 左ボタンが押された
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				// マウスの位置からチップの座標を算出する
				System.Drawing.Point chipPos = PositionToChipPosition((int)e.GetPosition(floorImage).X, (int)e.GetPosition(floorImage).Y);

				CroppedBitmap cb = new CroppedBitmap(
				   (BitmapSource)floorImage.Source,
				   new Int32Rect(32 * (chipPos.X / 32), 0, 32, 32));

				// 描画する
				selectImage.Source = cb;

				// 床のチップを選択した
				selectID = SelectID.Floor;
				// 選択しているチップ番号を記憶
				selectChip = chipPos.X / chipSize;
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// ステージのイメージを作成
			stageImage.Source = new BitmapImage();

			// 選択チップのイメージを作成
			selectImage.Source = new BitmapImage();

			// 配列を作成
			stageTable = new int[stageW * stageH];
			objectTable = new ObjectID[stageW * stageH];

			// ステージの更新
			UpdateStage();
		}


		private Bitmap ToBitmap(BitmapSource bitmapSource, System.Drawing.Imaging.PixelFormat pixelFormat)
		{
			int width = bitmapSource.PixelWidth;
			int height = bitmapSource.PixelHeight;
			int stride = width * ((bitmapSource.Format.BitsPerPixel + 7) / 8);  // 行の長さは色深度によらず8の倍数のため
			IntPtr intPtr = IntPtr.Zero;
			try
			{
				intPtr = Marshal.AllocCoTaskMem(height * stride);
				bitmapSource.CopyPixels(new Int32Rect(0, 0, width, height), intPtr, height * stride, stride);
				using (var bitmap = new Bitmap(width, height, stride, pixelFormat, intPtr))
				{
					// IntPtrからBitmapを生成した場合、Bitmapが存在する間、AllocCoTaskMemで確保したメモリがロックされたままとなる
					// （FreeCoTaskMemするとエラーとなる）
					// そしてBitmapを単純に開放しても解放されない
					// このため、明示的にFreeCoTaskMemを呼んでおくために一度作成したBitmapから新しくBitmapを
					// 再作成し直しておくとメモリリークを抑えやすい
					return new Bitmap(bitmap);
				}
			}
			finally
			{
				if (intPtr != IntPtr.Zero)
					Marshal.FreeCoTaskMem(intPtr);
			}
		}

		// ステージの更新メソッド
		private void UpdateStage()
		{
			// グラフィックスの作成
			WriteableBitmap processedBitmap = new WriteableBitmap((int)stageImage.Width, (int)stageImage.Height, 96, 96, PixelFormats.Bgr32, null);

			processedBitmap.Lock();

			// 床を描画する
			for (int i = 0; i < stageW * stageH; ++i)
			{
				unsafe
				{
					byte* ptr = (byte*)processedBitmap.BackBuffer;

					BitmapSource bs = (BitmapSource)floorImage.Source;
					Bitmap bitmap = ToBitmap(bs, System.Drawing.Imaging.PixelFormat.Format32bppRgb);

					for (int y = i / stageW * chipSize; y < (i / stageW * chipSize) + 32; ++y)
					{
						for (int x = i % stageW * chipSize; x < (i % stageW * chipSize) + 32; ++x)
						{
							System.Drawing.Color color = bitmap.GetPixel(x - (i % stageW * chipSize) + (32 * stageTable[i]), y - (i / stageW * chipSize));

							byte* pb = ptr + (y * processedBitmap.BackBufferStride) + (x * 4);

							pb[0] = color.B;
							pb[1] = color.G;
							pb[2] = color.R;
							//p[3] = 0;
						}
					}
				}

				// 床の上のオブジェクトを描画する
				if (objectTable[i] != ObjectID.None)
				{
					if (objectTable[i] < ObjectID.Enemy)
					{
						// プレイヤー＆パーツの場合
						unsafe
						{
							byte* ptr = (byte*)processedBitmap.BackBuffer;

							BitmapSource bs = (BitmapSource)playerPartsImage.Source;
							Bitmap bitmap = ToBitmap(bs, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

							for (int y = i / stageW * chipSize; y < (i / stageW * chipSize) + 32; ++y)
							{
								for (int x = i % stageW * chipSize; x < (i % stageW * chipSize) + 32; ++x)
								{
									System.Drawing.Color color = bitmap.GetPixel(x - (i % stageW * chipSize) + (32 * (int)objectTable[i]), y - (i / stageW * chipSize));

									byte* pb = ptr + (y * processedBitmap.BackBufferStride) + (x * 4);

									if (color.A != 0)
									{
										pb[0] = color.B;
										pb[1] = color.G;
										pb[2] = color.R;
										pb[3] = color.A;
									}
								}
							}
						}
					}
					else
					{
						// 敵の場合
						unsafe
						{
							byte* ptr = (byte*)processedBitmap.BackBuffer;

							BitmapSource bs = (BitmapSource)enemyImage.Source;
							Bitmap bitmap = ToBitmap(bs, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

							for (int y = i / stageW * chipSize; y < (i / stageW * chipSize) + 32; ++y)
							{
								for (int x = i % stageW * chipSize; x < (i % stageW * chipSize) + 32; ++x)
								{
									System.Drawing.Color color = bitmap.GetPixel(x - (i % stageW * chipSize) + (32 * ((int)objectTable[i] - (int)ObjectID.Enemy)), y - (i / stageW * chipSize));

									byte* pb = ptr + (y * processedBitmap.BackBufferStride) + (x * 4);
									if (color.A != 0)
									{
										pb[0] = color.B;
										pb[1] = color.G;
										pb[2] = color.R;
										pb[3] = color.A;
									}
								}
							}
						}
					}
				}

				processedBitmap.AddDirtyRect(new Int32Rect(i % stageW * chipSize, i / stageW * chipSize, 32, 32));
			}

			processedBitmap.Unlock();

			// 画面を更新する
			stageImage.Source = processedBitmap;
		}



		private void playerPartsImage_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// 左ボタンが押された
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				// マウスの位置からチップの座標を算出する
				System.Drawing.Point chipPos = PositionToChipPosition((int)e.GetPosition(playerPartsImage).X, (int)e.GetPosition(playerPartsImage).Y);

				CroppedBitmap cb = new CroppedBitmap(
				   (BitmapSource)playerPartsImage.Source,
				   new Int32Rect(32 * (chipPos.X / 32), 0, 32, 32));

				// 描画する
				selectImage.Source = cb;

				// プレイヤー＆パーツのチップを選択した
				selectID = SelectID.PlayerParts;
				// 選択しているチップ番号を記憶
				selectChip = chipPos.X / chipSize;
			}
		}

		private void enemyImage_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// 左ボタンが押された
			if (e.LeftButton == MouseButtonState.Pressed)
			{

				// マウスの位置からチップの座標を算出する
				System.Drawing.Point chipPos = PositionToChipPosition((int)e.GetPosition(enemyImage).X, (int)e.GetPosition(enemyImage).Y);

				CroppedBitmap cb = new CroppedBitmap(
				   (BitmapSource)enemyImage.Source,
				   new Int32Rect(32 * (chipPos.X / 32), 0, 32, 32));

				// 描画する
				selectImage.Source = cb;

				// 敵のチップを選択した
				selectID = SelectID.Enemy;
				// 選択しているチップ番号を記憶
				selectChip = chipPos.X / chipSize + (int)ObjectID.Enemy;
			}
		}


		private void stageImage_MouseDown(object sender, MouseButtonEventArgs e)
		{
			System.Drawing.Point chipPos = PositionToChipPosition((int)e.GetPosition(stageImage).X, (int)e.GetPosition(stageImage).Y);
			// マウスの位置がピクチャーボックス外なら無視する
			if (chipPos.X < 0 || chipPos.X >= stageImage.Width) return;
			if (chipPos.Y < 0 || chipPos.Y >= stageImage.Height) return;

			// 左ボタンが押された
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				// どこのチップかマウスの位置から算出する
				int num = chipPos.X / chipSize + chipPos.Y / chipSize * stageW;
				if (selectID == SelectID.Floor)
				{
					// オブジェクトが置いてあれば床は変更できない
					if (objectTable[num] == ObjectID.None)
					{
						// ステージに選んだチップを置く
						stageTable[num] = selectChip;
					}
				}
				else
				{
					// ステージ上のオブジェクトを配置する

					// 床がない場所は置けない
					if (stageTable[num] != 0)
					{
						// プレイヤーを置く場合は一回消しておく
						if (selectID == SelectID.PlayerParts && selectChip == (int)ObjectID.Player)
						{
							DeletePlayer();
						}
						objectTable[num] = (ObjectID)selectChip;
					}
				}
				// 画面を再描画する
				UpdateStage();
			}
		}

		private void DeletePlayer()
		{
			// プレイヤーを全て消す
			for (int i = 0; i < stageW * stageH; i++)
			{
				if (objectTable[i] == ObjectID.Player)
				{
					objectTable[i] = ObjectID.None;
				}
			}
		}

		private void resetButton_MouseDown(object sender, MouseButtonEventArgs e)
		{               // 全て消す
			for (int i = 0; i < stageW * stageH; i++)
			{
				stageTable[i] = 0;
				objectTable[i] = ObjectID.None;
			}
			// 画面を再描画する
			UpdateStage();
		}

		private void saveButton_MouseDown(object sender, MouseButtonEventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();

			// ダイアログを開く
			if (saveFileDialog.ShowDialog() == true)
			{
				string full = saveFileDialog.FileName + ".md";
				// ファイルを開く
				using (FileStream fs = new FileStream(
					full, FileMode.Create, FileAccess.Write))
				{
					Encoding sjisEnc = Encoding.GetEncoding("Shift_JIS");
					StreamWriter writer = new StreamWriter(fs, sjisEnc);

					// ヘッダ情報を書き出す
					writer.WriteLine("STAGE");

					// ステージデータをセーブする
					for (int j = 0; j < stageH; j++)
					{
						string str = "";
						for (int i = 0; i < stageW; i++)
						{
							str += stageTable[j * stageW + i].ToString() + ",";
						}
						writer.WriteLine(str);
					}

					// ヘッダ情報を書き出す
					writer.WriteLine("OBJECT");

					// 床の上のオブジェクトデータをセーブする
					for (int j = 0; j < stageH; j++)
					{
						string str = "";
						for (int i = 0; i < stageW; i++)
						{
							str += ((int)objectTable[j * stageW + i]).ToString() + ",";
						}
						writer.WriteLine(str);
					}

					// クローズする
					writer.Close();
				}
			}
		}

		private void loadButton_MouseDown(object sender, MouseButtonEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();

			// ダイアログを開く
			if (openFileDialog.ShowDialog() == true)
			{
				// ファイルを開く
				using (FileStream fs = new FileStream(
					openFileDialog.FileName, FileMode.Open))
				{
					Encoding sjisEnc = Encoding.GetEncoding("Shift_JIS");
					StreamReader reader = new StreamReader(fs, sjisEnc);

					// ヘッダまでシークする
					while (true)
					{
						string str = reader.ReadLine();
						if (str == "STAGE") break;
					}

					// ステージデータを読み込む
					for (int j = 0; j < stageH; j++)
					{
						// 1行読み込む
						string str = reader.ReadLine();
						// カンマでスプリット（分割）する
						string[] arr = str.Split(',');
						for (int i = 0; i < stageW; i++)
						{
							stageTable[j * stageW + i] = int.Parse(arr[i]);
						}
					}

					// ファイルポインタを先頭にシークする
					reader.BaseStream.Seek(0, SeekOrigin.Begin);

					// ヘッダまでシークする
					while (true)
					{
						string str = reader.ReadLine();
						if (str == "OBJECT") break;
					}

					// 床の上のオブジェクトデータを読み込む
					for (int j = 0; j < stageH; j++)
					{
						// 1行読み込む
						string str = reader.ReadLine();
						// カンマでスプリット（分割）する
						string[] arr = str.Split(',');
						for (int i = 0; i < stageW; i++)
						{
							objectTable[j * stageW + i] = (ObjectID)int.Parse(arr[i]);
						}
					}

					// クローズ
					reader.Close();

					// ステージを更新する
					UpdateStage();
				}
			}
		}
	}
}
