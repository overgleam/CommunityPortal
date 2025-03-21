document.addEventListener('DOMContentLoaded', function () {
    console.log("Dark mode script initialized");

    // Get the toggle element
    const darkModeToggle = document.getElementById('darkModeToggle');

    if (!darkModeToggle) {
        console.error("Dark mode toggle element not found!");
        return;
    }

    // Function to enable dark mode
    function enableDarkMode() {
        document.body.classList.add('dark-mode');
        darkModeToggle.checked = true;
        localStorage.setItem('darkMode', 'enabled');
        console.log("Dark mode enabled");
    }

    // Function to disable dark mode
    function disableDarkMode() {
        document.body.classList.remove('dark-mode');
        darkModeToggle.checked = false;
        localStorage.setItem('darkMode', 'disabled');
        console.log("Dark mode disabled");
    }

    // Check if user preference exists in local storage
    const darkModeStorage = localStorage.getItem('darkMode');
    console.log("Current dark mode storage value:", darkModeStorage);

    // Set initial state based on local storage or system preference
    if (darkModeStorage === 'enabled') {
        enableDarkMode();
    } else if (darkModeStorage === 'disabled') {
        disableDarkMode();
    } else {
        // If no preference is stored, check system preference
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            console.log("System prefers dark mode");
            enableDarkMode();
        } else {
            disableDarkMode();
        }
    }

    // Add event listener to toggle button
    darkModeToggle.addEventListener('change', function () {
        console.log("Toggle changed to:", this.checked);
        if (this.checked) {
            enableDarkMode();
        } else {
            disableDarkMode();
        }
    });
});