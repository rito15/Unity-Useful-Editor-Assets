using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 날짜 : 2021-08-27 AM 1:46:19
// 작성자 : Rito

namespace Rito
{
    /// <summary> 
    /// 재밌고 짜릿한 인스펙터 테트리스
    /// </summary>
    public class InspectorTetris : MonoBehaviour
    {
#if UNITY_EDITOR
        [System.Serializable]
        public class Tile
        {
            public bool hasBlock = false;
            public Color color = Color.white;
            public Rect rect;
        }

        [System.Serializable]
        public class Map
        {
            public const float TileSize = 20f;
            public const float TileGap = 2f; // 타일 간의 여백
            public static readonly Color GridColor = Color.red;

            private Editor editor;

            [SerializeField] private Vector2 leftTopPosition; // 좌상단 기준 좌표
            [SerializeField] private Vector2 leftBotPosition; // 좌하단 기준 좌표

            [SerializeField] private float mapWidth; // 맵 전체 너비
            [SerializeField] private float mapHeight; // 맵 전체 높이

            [SerializeField] private Tile[,] tiles;
            [SerializeField] private List<Rect> gridRects;
            [SerializeField] private Hero hero;
            
            [field: SerializeField]
            public int XCount { get; private set; }

            [field: SerializeField]
            public int YCount { get; private set; }

            public Map(Editor editor, int xCount, int yCount, Vector2 leftTopPosition)
            {
                this.editor = editor;

                this.XCount = xCount;
                this.YCount = yCount;

                this.mapWidth  = xCount * TileSize + ((xCount - 1) * TileGap);
                this.mapHeight = yCount * TileSize + ((yCount - 1) * TileGap);

                this.leftTopPosition = leftTopPosition;

                // 좌상단 좌표 이용해서 좌하단 좌표 계산
                this.leftBotPosition = 
                    new Vector2(leftTopPosition.x, leftTopPosition.y + this.mapHeight);

                this.tiles = new Tile[xCount, yCount];
                this.gridRects = new List<Rect>((xCount + 1) + (yCount + 1));
                this.hero = new Hero(xCount - 1);

                InitalizeMap();
                InitalizeGridRects();
                ResetHeroPosition();
            }

            /// <summary> 초기 맵 생성 </summary>
            private void InitalizeMap()
            {
                for (int y = 0; y < XCount; y++)
                {
                    for (int x = 0; x < XCount; x++)
                    {
                        tiles[x, y] = new Tile();
                        tiles[x, y].hasBlock = false;
                        tiles[x, y].rect = GetRectFromIndexPos(x, y);
                    }
                }

                tiles[0, 0].hasBlock = true;
                tiles[1, 0].hasBlock = true;
                tiles[2, 0].hasBlock = true;
                tiles[0, 1].hasBlock = true;
                tiles[1, 1].hasBlock = true;
                tiles[2, 1].hasBlock = true;
            }

            /// <summary> 그리드의 모든 렉트 정보 생성하기 </summary>
            private void InitalizeGridRects()
            {
                Rect horizontalRect = new Rect();
                horizontalRect.width = mapWidth + TileGap * 2;
                horizontalRect.height = TileGap;
                horizontalRect.position = leftTopPosition;
                horizontalRect.x -= TileGap;
                horizontalRect.y -= TileGap;

                Rect verticalRect = new Rect();
                verticalRect.width = TileGap;
                verticalRect.height = mapHeight + TileGap;
                verticalRect.position = leftTopPosition;
                verticalRect.x -= TileGap;

                for (int x = 0; x <= XCount; x++)
                {
                    if(x > 0)
                        verticalRect.x += TileSize + TileGap;
                    gridRects.Add(verticalRect);
                }

                for (int y = 0; y <= YCount; y++)
                {
                    if (y > 0)
                        horizontalRect.y += TileSize + TileGap;
                    gridRects.Add(horizontalRect);
                }
            }

            /// <summary> 히어로를 초기 위치로 지정 </summary>
            private void ResetHeroPosition()
            {
                this.hero.SetPosition(XCount / 2, YCount - 1);
            }

            // [x, y] 꼴 정수 인덱스로부터 Rect 얻기
            public Rect GetRectFromIndexPos(int x, int y)
            {
                Rect rect = new Rect();
                rect.width = TileSize;
                rect.height = TileSize;
                rect.x = leftBotPosition.x + (x * TileSize)       + (x * TileGap);
                rect.y = leftBotPosition.y - ((y + 1) * TileSize) - (y * TileGap);
                return rect;
            }

            /// <summary> 해당 x 좌표 위치에서 블록을 놓을 수 있는 가장 높은 y 좌표 구하기 </summary>
            public int GetPeakY(int x)
            {
                int y = 0;
                while (tiles[x, y].hasBlock) y++;
                return y;
            }

            public void Update()
            {
                ReserveEditorHeight();
                RenderGrid();
                RenderAllTilesWithBlock();

                UpdateHero();
                RenderHero();
            }

            /// <summary> 에디터 높이 확보하기 </summary>
            private void ReserveEditorHeight()
            {
                GUILayoutUtility.GetRect(1f, leftBotPosition.y + 10f);
            }

            /// <summary> 맵의 격자 그리기 </summary>
            private void RenderGrid()
            {
                foreach (var rect in gridRects)
                {
                    EditorGUI.DrawRect(rect, GridColor);
                }
            }

            /// <summary> 블록을 갖고 있는 타일들 그려주기 </summary>
            private void RenderAllTilesWithBlock()
            {
                // Note : X축(좌->우) 순회하면서
                // 각각 X좌표마다 하단(Y좌표 0)부터 상단으로 순회,
                // 블록이 있으면 Y축 이동 유지하고, 없으면 바로 우로 이동
                for (int x = 0; x < XCount; x++)
                {
                    int y = 0;
                    while (y < YCount && tiles[x, y].hasBlock)
                    {
                        RenderRectangle(x, y);
                        y++;
                    }
                }
            }

            /// <summary> 해당 Index Pos에 렉트 그려주기 </summary>
            private void RenderRectangle(int x, int y)
            {
                EditorGUI.DrawRect(tiles[x, y].rect, tiles[x, y].color);
            }
            private void RenderRectangle(int x, int y, in Color color)
            {
                EditorGUI.DrawRect(tiles[x, y].rect, color);
            }

            /// <summary> 히어로님 그려주기 </summary>
            private void RenderHero()
            {
                RenderRectangle(hero.x, hero.y, hero.HeroColor);
            }

            private void UpdateHero()
            {
                Event current = Event.current;

                if (current.type == EventType.KeyDown)
                {
                    switch (current.keyCode)
                    {
                        case KeyCode.LeftArrow:
                            // 좌측에 블록이 막고 있으면 이동 불가
                            if (hero.x == 0 || tiles[hero.x - 1, hero.y].hasBlock) break;

                            hero.MoveLeft();
                            break;

                        case KeyCode.RightArrow:
                            // 우측에 블록이 막고 있으면 이동 불가
                            if (hero.x == hero.xMax || tiles[hero.x + 1, hero.y].hasBlock) break;

                            hero.MoveRight();
                            break;

                        case KeyCode.DownArrow:
                            hero.MoveDown();
                            break;

                        case KeyCode.Space:
                            hero.MoveToFloor(GetPeakY(hero.x));
                            break;
                    }
                    editor.Repaint();
                }

                // 히어로가 바닥에 도착한 경우
                // 또는 히어로 바로 아래 위치에 블록이 존재할 경우
                // - 블록 쌓고 히어로 초기화
                if (hero.y == 0 || tiles[hero.x, hero.y - 1].hasBlock)
                {
                    tiles[hero.x, hero.y].hasBlock = true;
                    ResetHeroPosition();
                    editor.Repaint();
                }
            }
        }

        /// <summary> 사용자가 움직일 블럭 </summary>
        [System.Serializable]
        public class Hero
        {
            public int x, y;
            public readonly Color HeroColor = Color.cyan;

            public int xMax; // 최대 움직임 가능 인덱스 범위

            public Hero(int xMax)
            {
                this.xMax = xMax;
            }

            /// <summary> 히어로 위치 설정 </summary>
            public void SetPosition(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public void MoveLeft()
            {
                if (x > 0)
                    x--;
            }
            public void MoveRight()
            {
                if (x < xMax)
                    x++;
            }
            public void MoveDown()
            {
                if (y > 0)
                    y--;
            }

            /// <summary> 바닥으로 곤두박질 </summary>
            public void MoveToFloor(int floorY)
            {
                y = floorY;
            }
        }

        /***********************************************************************
        *                               Fields
        ***********************************************************************/
        #region .
        public Map map;// = new Map(10, 40, new Vector2(10f, 20f));

        #endregion

        [CustomEditor(typeof(InspectorTetris))]
        private class CE : UnityEditor.Editor
        {
            private InspectorTetris m;

            private void OnEnable()
            {
                m = target as InspectorTetris;
                m.map = new Map(this, 20, 20, new Vector2(10f, 20f));
            }

            public override void OnInspectorGUI()
            {
                m.map.Update();
            }
        }
#endif
    }
}