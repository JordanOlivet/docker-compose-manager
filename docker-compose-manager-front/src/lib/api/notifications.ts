import { apiClient } from './client';

export interface TestNotificationRequest {
  webhookUrl?: string;
}

const notificationsApi = {
  /**
   * Send a test Discord notification. If a webhook URL is provided it is tested
   * (lets the user verify before saving), otherwise the saved one is used.
   */
  testDiscord: async (webhookUrl?: string): Promise<void> => {
    await apiClient.post('/notifications/test', { webhookUrl } as TestNotificationRequest);
  },
};

export default notificationsApi;
