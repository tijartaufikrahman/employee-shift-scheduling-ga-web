/* =======================================================
   SisJad Admin JavaScript Core
   Ported & Enhanced from Project 7 SaaS Architecture
   ======================================================= */

document.addEventListener("DOMContentLoaded", function () {
    var sidebar = document.getElementById("sidebar");
    var mainContent = document.getElementById("mainContent");
    var overlay = document.getElementById("sidebarOverlay");
    var menuToggle = document.getElementById("menuToggle");
    var topbar = document.querySelector(".topbar");

    function isMobile() { return window.innerWidth <= 991; }

    function setSidebarState(open) {
        if (isMobile()) {
            if (open) {
                sidebar && sidebar.classList.remove("closed");
                overlay && overlay.classList.add("show");
            } else {
                sidebar && sidebar.classList.add("closed");
                overlay && overlay.classList.remove("show");
            }
        } else {
            if (open) {
                sidebar && sidebar.classList.remove("closed");
                mainContent && mainContent.classList.remove("sidebar-closed");
                if (topbar) {
                    topbar.classList.remove("sidebar-closed");
                    topbar.style.left = "";
                }
            } else {
                sidebar && sidebar.classList.add("closed");
                mainContent && mainContent.classList.add("sidebar-closed");
                if (topbar) {
                    topbar.classList.add("sidebar-closed");
                    topbar.style.left = "0";
                }
            }
        }
    }

    if (menuToggle) {
        menuToggle.addEventListener("click", function () {
            var isOpen = sidebar && !sidebar.classList.contains("closed");
            setSidebarState(!isOpen);
        });
    }

    if (overlay) {
        overlay.addEventListener("click", function () {
            setSidebarState(false);
        });
    }

    if (isMobile()) {
        setSidebarState(false);
    }

    window.addEventListener("resize", function () {
        if (!isMobile()) {
            setSidebarState(true);
            overlay && overlay.classList.remove("show");
        }
    });
});

// Toast notification helper
window.AdminToast = {
    _container: null,
    _getContainer: function() {
        if (!this._container) {
            this._container = document.createElement('div');
            this._container.className = 'admin-toast-wrap';
            document.body.appendChild(this._container);
        }
        return this._container;
    },
    show: function(msg, type) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: type || 'success',
                title: msg,
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true
            });
        } else {
            alert(msg);
        }
    },
    success: function(msg) { this.show(msg, 'success'); },
    error: function(msg) { this.show(msg, 'error'); }
};

// Global escape utility
window.escHtml = function (str) {
    if (!str && str !== 0) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
};
