/**
 * SignalR Connection Manager
 * Provides functionality to manage SignalR connections across the application.
 * Handles proper connection lifecycle including stopping connections during logout.
 */

// Store references to active connections
window.signalRConnections = window.signalRConnections || {
    chatConnection: null,
    notificationConnection: null
};

/**
 * Initializes the SignalR connection management
 * Sets up logout handler to properly clean up connections
 */
function initializeSignalRConnectionManager() {
    const logoutForm = document.getElementById('logoutForm');
    if (logoutForm) {
        logoutForm.addEventListener('submit', stopAllSignalRConnections);
    }
}

/**
 * Stops all active SignalR connections
 * Clears related storage
 */
function stopAllSignalRConnections() {
    console.log('Stopping all SignalR connections...');
    
    // Stop chat connection if it exists
    if (window.signalRConnections.chatConnection) {
        window.signalRConnections.chatConnection.stop()
            .catch(err => console.error('Error stopping chat connection:', err));
        window.signalRConnections.chatConnection = null;
    }
    
    // Stop notification connection if it exists
    if (window.signalRConnections.notificationConnection) {
        window.signalRConnections.notificationConnection.stop()
            .catch(err => console.error('Error stopping notification connection:', err));
        window.signalRConnections.notificationConnection = null;
    }
    
    // Clear any stored chat data
    sessionStorage.removeItem('chatState');
    localStorage.removeItem('chatMessages');
    
    // Clear previous connections
    if (window.previousChatConnection) {
        window.previousChatConnection.stop()
            .catch(err => console.error('Error stopping previous chat connection:', err));
        window.previousChatConnection = null;
    }
}

/**
 * Creates a managed chat connection
 * @param {string} hubUrl - URL to the SignalR hub
 * @returns {signalR.HubConnection} - The managed SignalR connection
 */
function createChatConnection(hubUrl) {
    // Stop existing connection if present
    if (window.signalRConnections.chatConnection) {
        window.signalRConnections.chatConnection.stop()
            .catch(err => console.error('Error stopping existing chat connection:', err));
    }
    
    // Create new connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .build();
    
    // Store reference
    window.signalRConnections.chatConnection = connection;
    
    return connection;
}

/**
 * Creates a managed notification connection
 * @param {string} hubUrl - URL to the SignalR hub
 * @returns {signalR.HubConnection} - The managed SignalR connection
 */
function createNotificationConnection(hubUrl) {
    // Stop existing connection if present
    if (window.signalRConnections.notificationConnection) {
        window.signalRConnections.notificationConnection.stop()
            .catch(err => console.error('Error stopping existing notification connection:', err));
    }
    
    // Create new connection with automatic reconnect
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();
    
    // Store reference
    window.signalRConnections.notificationConnection = connection;
    
    return connection;
}

// Initialize the connection manager when the document is ready
document.addEventListener('DOMContentLoaded', initializeSignalRConnectionManager); 