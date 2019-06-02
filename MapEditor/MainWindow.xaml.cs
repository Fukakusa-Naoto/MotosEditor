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
			FLOOR,
			PLAYER_PARTS,
			ENEMY,
		}

		// オブジェクトID
		enum ObjectID : int
		{
			NONE,
			PLAYER,
			POWER,
			JUMP,
			ENEMY
		}

		// <メンバ変数>
		int m_chipSize = 32;  // チップのサイズ
		int m_stageWidth = 12;    // ステージの横のチップ数
		int m_stageHeight = 12;    // ステージの縦のチップ数
		int[] m_stageTable;   // ステージデータの配列
		ObjectID[] m_objectTable; // 配置オブジェクトデータの配列
		int m_selectChip = 0; // 選択されているチップ番号
		SelectID m_selectID = SelectID.FLOOR; // 選択されているチップの種類


		public MainWindow()
		{
			InitializeComponent();
		}


		private System.Drawing.Point PositionToChipPosition(int x, int y)
		{
			return new System.Drawing.Point(x / m_chipSize * m_chipSize, y / m_chipSize * m_chipSize);
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
				m_selectID = SelectID.FLOOR;
				// 選択しているチップ番号を記憶
				m_selectChip = chipPos.X / m_chipSize;
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// ステージのイメージを作成
			stageImage.Source = new BitmapImage();

			// 選択チップのイメージを作成
			selectImage.Source = new BitmapImage();

			// 配列を作成
			m_stageTable = new int[m_stageWidth * m_stageHeight];
			m_objectTable = new ObjectID[m_stageWidth * m_stageHeight];

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
			for (int i = 0; i < m_stageWidth * m_stageHeight; ++i)
			{
				unsafe
				{
					byte* ptr = (byte*)processedBitmap.BackBuffer;

					BitmapSource bs = (BitmapSource)floorImage.Source;
					Bitmap bitmap = ToBitmap(bs, System.Drawing.Imaging.PixelFormat.Format32bppRgb);

					for (int y = i / m_stageWidth * m_chipSize; y < (i / m_stageWidth * m_chipSize) + 32; ++y)
					{
						for (int x = i % m_stageWidth * m_chipSize; x < (i % m_stageWidth * m_chipSize) + 32; ++x)
						{
							System.Drawing.Color color = bitmap.GetPixel(x - (i % m_stageWidth * m_chipSize) + (32 * m_stageTable[i]), y - (i / m_stageWidth * m_chipSize));

							byte* pb = ptr + (y * processedBitmap.BackBufferStride) + (x * 4);

							pb[0] = color.B;
							pb[1] = color.G;
							pb[2] = color.R;
							//p[3] = 0;
						}
					}
				}

				// 床の上のオブジェクトを描画する
				if (m_objectTable[i] != ObjectID.NONE)
				{
					if (m_objectTable[i] < ObjectID.ENEMY)
					{
						// プレイヤー＆パーツの場合
						unsafe
						{
							byte* ptr = (byte*)processedBitmap.BackBuffer;

							BitmapSource bs = (BitmapSource)playerPartsImage.Source;
							Bitmap bitmap = ToBitmap(bs, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

							for (int y = i / m_stageWidth * m_chipSize; y < (i / m_stageWidth * m_chipSize) + 32; ++y)
							{
								for (int x = i % m_stageWidth * m_chipSize; x < (i % m_stageWidth * m_chipSize) + 32; ++x)
								{
									System.Drawing.Color color = bitmap.GetPixel(x - (i % m_stageWidth * m_chipSize) + (32 * (int)m_objectTable[i]), y - (i / m_stageWidth * m_chipSize));

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

							for (int y = i / m_stageWidth * m_chipSize; y < (i / m_stageWidth * m_chipSize) + 32; ++y)
							{
								for (int x = i % m_stageWidth * m_chipSize; x < (i % m_stageWidth * m_chipSize) + 32; ++x)
								{
									System.Drawing.Color color = bitmap.GetPixel(x - (i % m_stageWidth * m_chipSize) + (32 * ((int)m_objectTable[i] - (int)ObjectID.ENEMY)), y - (i / m_stageWidth * m_chipSize));

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

				processedBitmap.AddDirtyRect(new Int32Rect(i % m_stageWidth * m_chipSize, i / m_stageWidth * m_chipSize, 32, 32));
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
				m_selectID = SelectID.PLAYER_PARTS;
				// 選択しているチップ番号を記憶
				m_selectChip = chipPos.X / m_chipSize;
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
				m_selectID = SelectID.ENEMY;
				// 選択しているチップ番号を記憶
				m_selectChip = chipPos.X / m_chipSize + (int)ObjectID.ENEMY;
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
				int num = chipPos.X / m_chipSize + chipPos.Y / m_chipSize * m_stageWidth;
				if (m_selectID == SelectID.FLOOR)
				{
					// オブジェクトが置いてあれば床は変更できない
					if (m_objectTable[num] == ObjectID.NONE)
					{
						// ステージに選んだチップを置く
						m_stageTable[num] = m_selectChip;
					}
				}
				else
				{
					// ステージ上のオブジェクトを配置する

					// 床がない場所は置けない
					if (m_stageTable[num] != 0)
					{
						// プレイヤーを置く場合は一回消しておく
						if (m_selectID == SelectID.PLAYER_PARTS && m_selectChip == (int)ObjectID.PLAYER)
						{
							DeletePlayer();
						}
						m_objectTable[num] = (ObjectID)m_selectChip;
					}
				}
				// 画面を再描画する
				UpdateStage();
			}
		}

		private void DeletePlayer()
		{
			// プレイヤーを全て消す
			for (int i = 0; i < m_stageWidth * m_stageHeight; i++)
			{
				if (m_objectTable[i] == ObjectID.PLAYER)
				{
					m_objectTable[i] = ObjectID.NONE;
				}
			}
		}

		private void resetButton_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// 全て消す
			for (int i = 0; i < m_stageWidth * m_stageHeight; i++)
			{
				m_stageTable[i] = 0;
				m_objectTable[i] = ObjectID.NONE;
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
					for (int j = 0; j < m_stageHeight; j++)
					{
						string str = "";
						for (int i = 0; i < m_stageWidth; i++)
						{
							str += m_stageTable[j * m_stageWidth + i].ToString() + ",";
						}
						writer.WriteLine(str);
					}

					// ヘッダ情報を書き出す
					writer.WriteLine("OBJECT");

					// 床の上のオブジェクトデータをセーブする
					for (int j = 0; j < m_stageHeight; j++)
					{
						string str = "";
						for (int i = 0; i < m_stageWidth; i++)
						{
							str += ((int)m_objectTable[j * m_stageWidth + i]).ToString() + ",";
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
					for (int j = 0; j < m_stageHeight; j++)
					{
						// 1行読み込む
						string str = reader.ReadLine();
						// カンマでスプリット（分割）する
						string[] arr = str.Split(',');
						for (int i = 0; i < m_stageWidth; i++)
						{
							m_stageTable[j * m_stageWidth + i] = int.Parse(arr[i]);
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
					for (int j = 0; j < m_stageHeight; j++)
					{
						// 1行読み込む
						string str = reader.ReadLine();
						// カンマでスプリット（分割）する
						string[] arr = str.Split(',');
						for (int i = 0; i < m_stageWidth; i++)
						{
							m_objectTable[j * m_stageWidth + i] = (ObjectID)int.Parse(arr[i]);
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
