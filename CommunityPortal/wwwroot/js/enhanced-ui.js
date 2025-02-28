/**
 * Enhanced UI JavaScript for Community Portal
 * This file handles all interactive UI elements including:
 * - Sidebar toggle functionality
 * - SweetAlert2 notifications
 * - Active link highlighting
 * - Notification management
 */

document.addEventListener('DOMContentLoaded', function() {
    // Initialize Bootstrap components
    initializeBootstrapComponents();
    
    // Setup sidebar functionality
    setupSidebar();
    
    // Display notifications using SweetAlert2
    displayNotifications();
    
    // Set active class on current page link in sidebar
    highlightActivePage();
    
    // Setup notification management
    setupNotificationSystem();
});

/**
 * Initialize Bootstrap components like tooltips and popovers
 */
function initializeBootstrapComponents() {
    // Initialize tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function(tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Initialize popovers
    const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    popoverTriggerList.map(function(popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl);
    });
}

/**
 * Setup sidebar toggle functionality for both mobile and desktop
 */
function setupSidebar() {
    // Mobile sidebar toggle
    setupMobileSidebar();
    
    // Desktop sidebar toggle
    setupDesktopSidebar();
}

/**
 * Setup mobile sidebar toggle functionality
 */
function setupMobileSidebar() {
    const sidebarToggler = document.getElementById('sidebar-toggler');
    const sidebarWrapper = document.getElementById('sidebar-wrapper');
    const sidebarOverlay = document.getElementById('sidebar-overlay');
    
    if (sidebarToggler) {
        sidebarToggler.addEventListener('click', function() {
            sidebarWrapper.classList.toggle('show');
            
            if (sidebarOverlay) {
                sidebarOverlay.classList.toggle('show');
            }
        });
    }
    
    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', function() {
            sidebarWrapper.classList.remove('show');
            sidebarOverlay.classList.remove('show');
        });
    }
}

/**
 * Setup desktop sidebar toggle functionality with localStorage persistence
 */
function setupDesktopSidebar() {
    const sidebarToggle = document.getElementById('sidebar-toggle');
    const wrapper = document.getElementById('wrapper');
    
    if (sidebarToggle) {
        // Check if sidebar state is stored in localStorage
        const sidebarState = localStorage.getItem('sidebarCollapsed');
        
        // Apply stored state on page load
        if (sidebarState === 'true') {
            wrapper.classList.add('sidebar-collapsed');
            sidebarToggle.querySelector('.material-icons').textContent = 'menu_open';
        }
        
        sidebarToggle.addEventListener('click', function() {
            wrapper.classList.toggle('sidebar-collapsed');
            
            // Update toggle icon based on sidebar state
            const isCollapsed = wrapper.classList.contains('sidebar-collapsed');
            sidebarToggle.querySelector('.material-icons').textContent = isCollapsed ? 'menu_open' : 'menu';
            
            // Store sidebar state in localStorage
            localStorage.setItem('sidebarCollapsed', isCollapsed);
        });
    }
}

/**
 * Display SweetAlert2 notifications for TempData messages
 */
function displayNotifications() {
    const successMessage = document.getElementById('tempDataSuccessMessage')?.value;
    const errorMessage = document.getElementById('tempDataErrorMessage')?.value;
    
    if (successMessage) {
        Swal.fire({
            title: 'Success!',
            text: successMessage,
            icon: 'success',
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 5000,
            timerProgressBar: true,
            showCloseButton: true,
            didOpen: (toast) => {
                toast.addEventListener('mouseenter', Swal.stopTimer);
                toast.addEventListener('mouseleave', Swal.resumeTimer);
            }
        });
    }
    
    if (errorMessage) {
        Swal.fire({
            title: 'Error!',
            text: errorMessage,
            icon: 'error',
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 5000,
            timerProgressBar: true,
            showCloseButton: true,
            didOpen: (toast) => {
                toast.addEventListener('mouseenter', Swal.stopTimer);
                toast.addEventListener('mouseleave', Swal.resumeTimer);
            }
        });
    }
}

/**
 * Highlight the active page in the sidebar and expand its parent accordion
 */
function highlightActivePage() {
    // Normalize the current path by removing any trailing slash (except for the root)
    let currentLocation = window.location.pathname;
    if (currentLocation !== "/" && currentLocation.endsWith("/")) {
        currentLocation = currentLocation.slice(0, -1);
    }

    const sidebarLinks = document.querySelectorAll('.sidebar-accordion .list-group-item');

    sidebarLinks.forEach(link => {
        let href = link.getAttribute('href');
        if (href) {
            // Normalize the href as well
            if (href !== "/" && href.endsWith("/")) {
                href = href.slice(0, -1);
            }

            // Use exact match (or you could extend to allow a trailing slash)
            if (currentLocation === href) {
                link.classList.add('active');

                // If the link is inside an accordion, expand its parent
                const accordionBody = link.closest('.accordion-collapse');
                if (accordionBody) {
                    accordionBody.classList.add('show');
                    const accordionButton = document.querySelector(`[data-bs-target="#${accordionBody.id}"]`);
                    if (accordionButton) {
                        accordionButton.classList.remove('collapsed');
                        accordionButton.setAttribute('aria-expanded', 'true');
                    }
                }
            }
        }
    });
}

/**
 * Setup notification system for marking notifications as read
 */
function setupNotificationSystem() {
    const markAsReadButtons = document.querySelectorAll('.mark-as-read');
    markAsReadButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.preventDefault();
            const notificationId = this.getAttribute('data-notification-id');
            // Here you would typically make an AJAX call to mark the notification as read
            console.log('Marking notification as read:', notificationId);
            
            // For demo purposes, just update the notification item appearance
            this.closest('.notification-item').style.opacity = '0.5';
            
            // Update badge count
            updateNotificationBadge();
        });
    });
    
    // "Mark all as read" functionality
    const markAllAsReadLink = document.querySelector('.dropdown-menu a[href="#"].text-decoration-none.small');
    if (markAllAsReadLink) {
        markAllAsReadLink.addEventListener('click', function(e) {
            e.preventDefault();
            
            // Mark all notification items as read
            document.querySelectorAll('.notification-item').forEach(item => {
                item.style.opacity = '0.5';
            });
            
            // Hide the badge
            const badge = document.querySelector('#notificationsDropdown .badge');
            if (badge) {
                badge.style.display = 'none';
            }
        });
    }
}

/**
 * Update the notification badge count
 */
function updateNotificationBadge() {
    const badge = document.querySelector('#notificationsDropdown .badge');
    if (badge) {
        const count = parseInt(badge.textContent);
        if (count > 0) {
            badge.textContent = count - 1;
        }
        if (count - 1 <= 0) {
            badge.style.display = 'none';
        }
    }
} 