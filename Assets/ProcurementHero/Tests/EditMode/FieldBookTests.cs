using System.Linq;
using NUnit.Framework;
using ProcurementHero.Field;
using UnityEngine;

namespace ProcurementHero.Tests
{
    /// <summary>
    /// 수첩 넘김·표지의 상태 정리: 내용 교체 콜백 횟수, 중단·건너뛰기 뒤 최종 페이지와 임시 오브젝트, 표지 열림/닫힘의 왼쪽 절반 위치.
    /// 애니메이션 시간·곡선 숫자는 검사하지 않는다(실제 재생으로 확인).
    /// </summary>
    public class FieldBookTests
    {
        GameObject root;
        FieldBook.Parts book;

        [SetUp]
        public void Make()
        {
            root = new GameObject("BookTestCanvas", typeof(RectTransform), typeof(Canvas));
            book = FieldBook.Build(root.transform, Vector2.zero, Vector2.one, true);
        }

        [TearDown]
        public void Clean() => Object.DestroyImmediate(root);

        static RectTransform Content(RectTransform page, string name)
        {
            var rt = UIKit.Rect(page, name);
            rt.Fill();
            return rt;
        }

        static bool Has(RectTransform page, string name) => page.Cast<Transform>().Any(t => t.name == name);
        bool AnyTemp() => root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("TurningPage") || t.name.StartsWith("TurnUnder"));

        [Test]
        public void TurnBuildsNextContentOnceAndFinishPutsItInPlace()
        {
            FieldBook.SetOpen(book);
            Content(book.left, "OldLeft");
            Content(book.right, "OldRight");
            int built = 0;
            var turn = FieldBook.TurnPage(book, () =>
            {
                built++;
                Content(book.left, "NewLeft");
                Content(book.right, "NewRight");
            });
            turn.MoveNext(); // 넘기기 시작 직전까지(내용 준비)
            Assert.AreEqual(1, built, "내용 교체 콜백은 시작할 때 한 번");
            Assert.IsTrue(book.Turning);
            Assert.IsFalse(Has(book.right, "OldRight"), "이전 오른쪽 내용은 넘어가는 장 앞면으로 옮겨짐");
            Assert.IsTrue(Has(book.right, "NewRight"), "새 오른쪽 내용은 제자리(넘어가는 장 밑)");
            Assert.IsFalse(Has(book.left, "NewLeft"), "새 왼쪽 내용은 장이 내려앉기 전까지 뒷면에 붙어 있음");

            // 건너뛰기·중단: 바로 끝낸 상태로 정리한다(여러 번 불러도 같음).
            FieldBook.FinishTurn(book);
            FieldBook.FinishTurn(book);
            Assert.IsFalse(book.Turning);
            Assert.IsTrue(Has(book.left, "NewLeft"));
            Assert.IsTrue(Has(book.right, "NewRight"));
            Assert.IsFalse(Has(book.left, "OldLeft"));
            Assert.IsFalse(AnyTemp(), "임시 장·받침 종이·그림자가 남지 않음");
            // 중단된 코루틴이 다시 돌아도 콜백을 되풀이하거나 오류를 내지 않는다.
            for (int i = 0; i < 3; i++) turn.MoveNext();
            Assert.AreEqual(1, built);
        }

        [Test]
        public void StartingAnotherTurnFinishesThePendingOneFirst()
        {
            FieldBook.SetOpen(book);
            int a = 0, b = 0;
            var first = FieldBook.TurnPage(book, () => { a++; Content(book.left, "A"); });
            first.MoveNext();
            var second = FieldBook.TurnPage(book, () => { b++; Content(book.left, "B"); });
            second.MoveNext();
            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
            FieldBook.FinishTurn(book);
            Assert.IsTrue(Has(book.left, "B"));
            Assert.IsFalse(Has(book.left, "A"), "앞선 넘김의 내용은 다음 넘김의 이전 내용으로 정리됨");
            Assert.IsFalse(AnyTemp());
        }

        [Test]
        public void CoverStatesKeepLeftHalfAndRestoreNormalTransform()
        {
            var leftHalf = book.left.parent;
            FieldBook.SetClosed(book);
            Assert.AreNotEqual(book.book, leftHalf.parent, "닫힘: 왼쪽 절반은 표지 뒷면에 붙어 있음");
            FieldBook.SetCover(book, 0.7f);
            Assert.AreNotEqual(book.book, leftHalf.parent);
            book.book.localScale = new Vector3(0.5f, 0.5f, 1);
            book.group.alpha = 0.3f;
            FieldBook.SetOpen(book);
            Assert.AreEqual(book.book, leftHalf.parent, "펼침: 왼쪽 절반이 책의 제자리로 돌아옴");
            Assert.AreEqual(0, leftHalf.GetSiblingIndex());
            Assert.AreEqual(Vector3.one, leftHalf.localScale);
            Assert.AreEqual(Vector3.one, book.book.localScale);
            Assert.AreEqual(1f, book.group.alpha);
            Assert.AreEqual(1f, book.bandGroup.alpha);
            Assert.AreEqual(1f, book.CoverOpen);
        }

        [Test]
        public void CoverChangeDuringTurnFinishesTheTurn()
        {
            FieldBook.SetOpen(book);
            var turn = FieldBook.TurnPage(book, () => Content(book.left, "Next"));
            turn.MoveNext();
            FieldBook.SetClosed(book); // 예: 넘기는 중 닫기
            Assert.IsFalse(book.Turning);
            Assert.IsTrue(Has(book.left, "Next"));
            Assert.IsFalse(AnyTemp());
        }
    }
}
