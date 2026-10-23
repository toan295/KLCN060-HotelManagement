// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Ép mọi input ngày hiển thị dd/mm/yyyy bất kể ngôn ngữ trình duyệt/hệ điều hành của người dùng.
// input[type=date] gốc của HTML5 LUÔN theo locale trình duyệt (thuộc tính lang="vi" trên <html> KHÔNG có
// tác dụng với riêng control này - đã kiểm chứng thực tế), nên các View đổi input này thành
// <input type="text" class="date-picker-vi" name="..."> rồi flatpickr tự vẽ lại: ô hiển thị luôn là
// dd/mm/yyyy, còn giá trị THẬT gửi lên server (qua input ẩn cùng "name") vẫn là yyyy-MM-dd như trước,
// không cần đổi gì ở Controller/ViewModel phía sau.
(function () {
    if (typeof flatpickr === 'undefined') return;
    document.querySelectorAll('input.date-picker-vi').forEach(function (el) {
        flatpickr(el, {
            locale: 'vn', disableMobile: true,
            dateFormat: 'Y-m-d',
            altInput: true,
            altFormat: 'd/m/Y',
            altInputClass: el.className.replace('date-picker-vi', '').trim(),
            minDate: el.dataset.min || undefined,
            allowInput: true
        });
    });
})();

// Thanh điều hướng: đổ bóng + thu gọn nhẹ khi cuộn xuống.
(function () {
    var header = document.querySelector('.site-header');
    if (!header) return;
    function capNhat() { header.classList.toggle('is-scrolled', window.scrollY > 8); }
    capNhat();
    window.addEventListener('scroll', capNhat, { passive: true });
})();

// Hiệu ứng "hiện dần" cho các khối có class .reveal khi cuộn tới (không có JS/IntersectionObserver thì hiện ngay).
(function () {
    var els = document.querySelectorAll('.reveal');
    if (!els.length) return;
    if (!('IntersectionObserver' in window)) {
        els.forEach(function (el) { el.classList.add('in'); });
        return;
    }
    var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
            if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); }
        });
    }, { threshold: 0.12, rootMargin: '0px 0px -40px 0px' });
    els.forEach(function (el) { io.observe(el); });
})();

// Thư viện ảnh ở trang chi tiết phòng: bấm ảnh nhỏ thì đổi ảnh lớn (chỉ áp dụng với ảnh đã có thật).
(function () {
    var main = document.querySelector('.gallery-main .photo');
    var thumbs = document.querySelectorAll('.gallery-thumbs .photo');
    if (!main || !thumbs.length) return;
    var mainImg = main.querySelector('.photo-img');
    var mainSrc = mainImg ? mainImg.getAttribute('src') : null;

    thumbs.forEach(function (t) {
        var img = t.querySelector('.photo-img');
        if (!img) { t.style.cursor = 'default'; return; }
        t.addEventListener('click', function () {
            var target = main.querySelector('.photo-img');
            if (!target) {
                target = document.createElement('img');
                target.className = 'photo-img';
                target.alt = img.alt;
                main.prepend(target);
            }
            target.src = img.getAttribute('src');
            thumbs.forEach(function (x) { x.classList.remove('is-active'); });
            t.classList.add('is-active');
        });
    });

    // Nút quay lại ảnh đại diện ban đầu khi bấm vào ảnh lớn.
    main.addEventListener('click', function () {
        var target = main.querySelector('.photo-img');
        if (target && mainSrc) { target.src = mainSrc; thumbs.forEach(function (x) { x.classList.remove('is-active'); }); }
    });
})();


// Ô mật khẩu: nút con mắt để hiện/ẩn mật khẩu + (với ô có data-strength) thanh đánh giá độ mạnh.
// Áp dụng tự động cho MỌI input[type=password] nên các trang đăng nhập/đăng ký/đổi/đặt lại mật khẩu dùng chung một hành vi.
(function () {
    var EYE = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>';
    var EYE_OFF = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>';

    function diem(v) {
        var s = 0;
        if (v.length >= 8) s++;
        if (v.length >= 12) s++;
        if (/[a-z]/.test(v) && /[A-Z]/.test(v)) s++;
        if (/\d/.test(v)) s++;
        if (/[^A-Za-z0-9]/.test(v)) s++;
        return s;
    }

    document.querySelectorAll('input[type="password"]').forEach(function (input) {
        if (input.closest('.pw-field')) return;

        var wrap = document.createElement('div');
        wrap.className = 'pw-field';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'pw-toggle';
        btn.setAttribute('aria-label', 'Hiện mật khẩu');
        btn.setAttribute('aria-pressed', 'false');
        btn.innerHTML = EYE;
        wrap.appendChild(btn);

        btn.addEventListener('click', function () {
            var hien = input.type === 'password';
            input.type = hien ? 'text' : 'password';
            btn.innerHTML = hien ? EYE_OFF : EYE;
            btn.setAttribute('aria-pressed', hien ? 'true' : 'false');
            btn.setAttribute('aria-label', hien ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
            input.focus();
        });

        // Gửi biểu mẫu thì luôn trả về dạng ẩn (không để trình duyệt/lịch sử giữ mật khẩu ở dạng chữ).
        if (input.form) input.form.addEventListener('submit', function () { input.type = 'password'; });

        if (input.hasAttribute('data-strength')) {
            var meter = document.createElement('div');
            meter.className = 'pw-meter';
            meter.innerHTML = '<div class="bar"><i></i></div><span class="txt"></span>';
            wrap.insertAdjacentElement('afterend', meter);
            var txt = meter.querySelector('.txt');
            input.addEventListener('input', function () {
                var v = input.value;
                if (!v) { meter.classList.remove('on'); return; }
                var d = diem(v);
                var muc = d <= 2 ? 1 : d <= 3 ? 2 : 3;
                meter.dataset.level = muc;
                txt.textContent = muc === 1 ? 'Yếu' : muc === 2 ? 'Trung bình' : 'Mạnh';
                meter.classList.add('on');
            });
        }
    });
})();
