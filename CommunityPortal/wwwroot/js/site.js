// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", function () {
    var prev = document.getElementById("prev");
    var next = document.getElementById("next");
    var thumbnails = document.getElementsByClassName("thumbnail");
    var hero = document.getElementById("hero");
    var title = document.querySelector(".info h1");
    var description = document.querySelector(".info p");
    var searchInput = document.getElementById("search");
    var getStartedBtn = document.getElementById("getStarted");
    var fadeBg = document.querySelector(".fade-bg");
    var suggestionsDiv = document.getElementById("suggestions"); // Ensure it's defined

    var content = [
        { image: "/images/bbg1.jpg", title: "Community Portal", text: "Your gateway to a seamless community experience—stay connected, informed, and engaged with everything happening in The Rise at Monterrazas." },
        { image: "/images/bbg2.jpg", title: "Advisory", text: "Stay up-to-date with the latest news, policy changes, and upcoming events in the community." },
        { image: "/images/bbg3.jpg", title: "Online Payments", text: "Easily pay your dues, reserve amenities, and track your financial transactions with our online system." },
        { image: "/images/bbg4.jpg", title: "Request Services", text: "Need repairs or maintenance? Submit a request and track its progress in real-time." },
        { image: "/images/bbg5.jpg", title: "Community Engagement", text: "Join discussions, share ideas, and connect with your neighbors to build a vibrant community." }
    ];

    let i = 0; // Track current slide index

    function updateContent() {
        if (!hero || !title || !description) return;

        // Apply fade effect
        fadeBg.style.backgroundImage = `url('${content[i].image}')`;
        fadeBg.style.opacity = "1";

        setTimeout(() => {
            hero.style.backgroundImage = `url('${content[i].image}')`;
            title.textContent = content[i].title;
            description.textContent = content[i].text;

            setTimeout(() => {
                fadeBg.style.opacity = "0";
            }, 300);
        }, 200);

        // Show or hide search & button
        if (i === 0) {
            searchInput.classList.remove("hidden");
            getStartedBtn.classList.add("hidden");
        } else {
            searchInput.classList.add("hidden");
            getStartedBtn.classList.remove("hidden");
        }

        // Update active thumbnail
        for (let j = 0; j < thumbnails.length; j++) {
            thumbnails[j].classList.remove("active");
        }
        if (thumbnails[i]) {
            thumbnails[i].classList.add("active");
        }
    }

    // Next button event listener
    if (next) {
        next.addEventListener("click", function () {
            i = (i + 1) % content.length; // Loop back to first slide
            updateContent();
        });
    }

    // Previous button event listener
    if (prev) {
        prev.addEventListener("click", function () {
            i = (i - 1 + content.length) % content.length; // Loop back to last slide
            updateContent();
        });
    }

    // Thumbnail click event listener
    if (thumbnails.length > 0) {
        for (let j = 0; j < thumbnails.length; j++) {
            thumbnails[j].addEventListener("click", function () {
                i = j;
                updateContent();
            });
        }
    }

    // Initialize the first content
    updateContent();

    // Search functionality
    var keywords = [
        { name: "Account", link: "/Profile/ViewProfile" },
        { name: "Advisory", link: "/" },
        { name: "Online Payments", link: "/Billing" },
        { name: "Request Services", link: "/ServiceRequest" },
        { name: "Community Engagement", link: "/Forum" },
        { name: "Announcements", link: "/Announcement" },
        { name: "Upcoming Events", link: "/Events" },
        { name: "View account balance", link: "/Billing" },
        { name: "Reserve clubhouse", link: "/Facility" },
        { name: "Swimming pool schedule", link: "community-engagement.html" },
        { name: "File a complaint", link: "/" },
        { name: "Submit feedback", link: "/Feedback/Create" }
    ];

    searchInput.addEventListener("input", function () {
        var query = searchInput.value.toLowerCase();
        suggestionsDiv.innerHTML = '';

        if (query) {
            var filteredKeywords = keywords.filter(function (keyword) {
                return keyword.name.toLowerCase().includes(query);
            });

            filteredKeywords.forEach(function (keyword) {
                var suggestionItem = document.createElement("div");
                suggestionItem.classList.add("suggestion-item");
                suggestionItem.textContent = keyword.name;
                suggestionItem.onclick = function () {
                    window.location.href = keyword.link;
                };
                suggestionsDiv.appendChild(suggestionItem);
            });
            suggestionsDiv.classList.remove("hidden");
        } else {
            suggestionsDiv.classList.add("hidden");
        }
    });
});
