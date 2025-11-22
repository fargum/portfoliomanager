'use client';

import React, { useState, useRef, useEffect } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Send, Bot, User, AlertCircle, TrendingUp, TrendingDown, Activity, Clock, Lightbulb, LogIn } from 'lucide-react';
import { apiClient } from '@/lib/api-client';
import { useAuth } from '@/contexts/AuthContext';
import { ChatMessage, ChatState, InsightDto } from '@/types/chat';

interface AiChatProps {
  accountId: number;
  className?: string;
  isVisible?: boolean;
}

export function AiChat({ accountId, className = '', isVisible = true }: AiChatProps) {
  const [inputValue, setInputValue] = useState('');
  const [currentThreadId, setCurrentThreadId] = useState<number | undefined>(undefined);
  const { isAuthenticated, login } = useAuth();
  const [chatState, setChatState] = useState<ChatState>({
    messages: [
      {
        id: '1',
        type: 'system',
        content: `Welcome! I'm your AI portfolio assistant. Ask me anything about your portfolio, such as:
        
• "How is my portfolio performing today?"
• "What are my top holdings?"
• "Show me market sentiment for my stocks"
• "Compare my performance from last week"
        
I can analyze your holdings, market conditions, and provide insights to help you make informed decisions.`,
        timestamp: new Date(),
      }
    ],
    isLoading: false,
  });
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [chatState.messages]);

  // Scroll to bottom when component becomes visible
  useEffect(() => {
    if (isVisible) {
      setTimeout(() => scrollToBottom(), 100);
    }
  }, [isVisible]);

  const generateMessageId = () => `msg_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!inputValue.trim() || chatState.isLoading) return;

    const userMessage: ChatMessage = {
      id: generateMessageId(),
      type: 'user',
      content: inputValue.trim(),
      timestamp: new Date(),
    };

    // Add user message and create streaming AI message
    const aiMessageId = generateMessageId();
    const loadingMessage: ChatMessage = {
      id: aiMessageId,
      type: 'ai',
      content: '',
      timestamp: new Date(),
      isLoading: true,
    };

    setChatState(prev => ({
      ...prev,
      messages: [...prev.messages, userMessage, loadingMessage],
      isLoading: true,
    }));

    const query = inputValue.trim();
    setInputValue('');

    // Try streaming first, fallback to regular if not supported
    let streamingContent = '';
    let streamingSupported = true;

    try {
      await apiClient.sendChatQueryStream(
        query,
        accountId,
        // On chunk received
        (chunk: string) => {
          streamingContent += chunk;
          setChatState(prev => ({
            ...prev,
            messages: prev.messages.map(msg => 
              msg.id === aiMessageId 
                ? { ...msg, content: streamingContent, isLoading: false }
                : msg
            ),
          }));
        },
        // On complete
        () => {
          setChatState(prev => ({
            ...prev,
            isLoading: false,
            error: undefined,
          }));
        },
        // On error - fallback to regular API
        async (error: string) => {
          console.warn('Streaming failed, falling back to regular API:', error);
          streamingSupported = false;
          
          // Fallback to regular API call
          try {
            const response = await apiClient.sendChatQuery(query, accountId, currentThreadId);
            
            if (response.error) {
              throw new Error(response.error);
            }

            // Update threadId if response includes it
            if (response.data?.threadId && !currentThreadId) {
              setCurrentThreadId(response.data.threadId);
            }

            const aiMessage: ChatMessage = {
              id: aiMessageId,
              type: 'ai',
              content: response.data?.response || 'I apologize, but I couldn\'t process your request.',
              timestamp: new Date(),
              queryType: response.data?.queryType,
              portfolioSummary: response.data?.portfolioSummary,
              insights: response.data?.insights,
            };

            setChatState(prev => ({
              ...prev,
              messages: prev.messages.map(msg => 
                msg.id === aiMessageId ? aiMessage : msg
              ),
              isLoading: false,
              error: undefined,
            }));

          } catch (fallbackError) {
            const errorMessage: ChatMessage = {
              id: aiMessageId,
              type: 'ai',
              content: `I apologize, but I encountered an error: ${fallbackError instanceof Error ? fallbackError.message : 'Unknown error'}`,
              timestamp: new Date(),
              error: fallbackError instanceof Error ? fallbackError.message : 'Unknown error',
            };

            setChatState(prev => ({
              ...prev,
              messages: prev.messages.map(msg => 
                msg.id === aiMessageId ? errorMessage : msg
              ),
              isLoading: false,
              error: fallbackError instanceof Error ? fallbackError.message : 'Unknown error',
            }));
          }
        },
        currentThreadId
      );
    } catch (error) {
      // If streaming is not supported, fall back to regular API
      if (!streamingSupported) {
        return; // Already handled in the error callback
      }

      const errorMessage: ChatMessage = {
        id: aiMessageId,
        type: 'ai',
        content: `I apologize, but I encountered an error: ${error instanceof Error ? error.message : 'Unknown error'}`,
        timestamp: new Date(),
        error: error instanceof Error ? error.message : 'Unknown error',
      };

      setChatState(prev => ({
        ...prev,
        messages: prev.messages.map(msg => 
          msg.id === aiMessageId ? errorMessage : msg
        ),
        isLoading: false,
        error: error instanceof Error ? error.message : 'Unknown error',
      }));
    }
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: 'GBP',
      minimumFractionDigits: 2,
    }).format(value);
  };

  const formatPercentage = (value: number) => {
    return `${value >= 0 ? '+' : ''}${(value * 100).toFixed(2)}%`;
  };

  const renderInsight = (insight: InsightDto, index: number) => (
    <div key={index} className="bg-blue-50 border border-blue-200 rounded-lg p-3 mt-2">
      <div className="flex items-start space-x-2">
        <Lightbulb className="h-4 w-4 text-blue-600 mt-0.5 flex-shrink-0" />
        <div className="flex-1">
          <div className="flex items-center space-x-2">
            <h4 className="font-medium text-blue-900 text-sm">{insight.title}</h4>
            <span className={`px-2 py-1 rounded-full text-xs font-medium ${
              insight.impact === 'High' ? 'bg-red-100 text-red-800' :
              insight.impact === 'Medium' ? 'bg-yellow-100 text-yellow-800' :
              'bg-green-100 text-green-800'
            }`}>
              {insight.impact}
            </span>
          </div>
          <p className="text-blue-800 text-sm mt-1">{insight.description}</p>
          <span className="text-blue-600 text-xs mt-1 block">{insight.category}</span>
        </div>
      </div>
    </div>
  );

  const renderPortfolioSummary = (summary: any) => (
    <div className="bg-gray-50 border border-gray-200 rounded-lg p-4 mt-3">
      <h4 className="font-semibold text-gray-900 mb-3 flex items-center">
        <Activity className="h-4 w-4 mr-2" />
        Portfolio Summary
      </h4>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <p className="text-sm text-gray-600">Total Value</p>
          <p className="font-bold text-lg">{formatCurrency(summary.totalValue)}</p>
        </div>
        <div>
          <p className="text-sm text-gray-600">Day Change</p>
          <p className={`font-bold text-lg flex items-center ${
            summary.dayChange >= 0 ? 'text-green-600' : 'text-red-600'
          }`}>
            {summary.dayChange >= 0 ? <TrendingUp className="h-4 w-4 mr-1" /> : <TrendingDown className="h-4 w-4 mr-1" />}
            {formatCurrency(summary.dayChange)} ({formatPercentage(summary.dayChangePercentage)})
          </p>
        </div>
        <div>
          <p className="text-sm text-gray-600">Holdings Count</p>
          <p className="font-bold">{summary.holdingsCount}</p>
        </div>
        <div>
          <p className="text-sm text-gray-600">Date</p>
          <p className="font-bold">{new Date(summary.date).toLocaleDateString()}</p>
        </div>
      </div>
      {summary.topHoldings && summary.topHoldings.length > 0 && (
        <div className="mt-3">
          <p className="text-sm text-gray-600 mb-2">Top Holdings</p>
          <div className="flex flex-wrap gap-2">
            {summary.topHoldings.map((holding: string, index: number) => (
              <span key={index} className="bg-blue-100 text-blue-800 px-2 py-1 rounded-full text-xs font-medium">
                {holding}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );

  const renderMessage = (message: ChatMessage) => {
    const isUser = message.type === 'user';
    const isSystem = message.type === 'system';
    
    return (
      <div key={message.id} className={`flex ${isUser ? 'justify-end' : 'justify-start'} mb-4`}>
        <div className={`flex items-start space-x-2 max-w-[95%] ${isUser ? 'flex-row-reverse space-x-reverse' : ''}`}>
          {/* Avatar */}
          <div className={`flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center ${
            isUser ? 'bg-blue-600 text-white' : 
            isSystem ? 'bg-gray-500 text-white' : 
            'bg-green-600 text-white'
          }`}>
            {isUser ? <User className="h-4 w-4" /> : <Bot className="h-4 w-4" />}
          </div>
          
          {/* Message Content */}
          <div className={`rounded-xl px-4 py-3 ${
            isUser ? 'bg-blue-600 text-white' : 
            isSystem ? 'bg-gray-100 text-gray-800 border' :
            message.error ? 'bg-red-50 text-red-800 border border-red-200' :
            'bg-white text-gray-800 border shadow-sm'
          }`}>
            {message.isLoading ? (
              <div className="flex items-center space-x-2">
                <div className="animate-spin h-4 w-4 border-2 border-gray-300 border-t-blue-600 rounded-full"></div>
                <span className="text-sm">Thinking...</span>
              </div>
            ) : (
              <>
                {message.error && (
                  <div className="flex items-center space-x-2 mb-2">
                    <AlertCircle className="h-4 w-4 text-red-600" />
                    <span className="text-sm font-medium text-red-800">Error</span>
                  </div>
                )}
                
                {/* Render content with markdown support */}
                <div className="max-w-none">
                  {isUser ? (
                    <div className="whitespace-pre-wrap text-white">{message.content}</div>
                  ) : (
                    <ReactMarkdown 
                      remarkPlugins={[remarkGfm]}
                      components={{
                        table: ({ children }) => (
                          <div className="chat-table-container">
                            <table className="chat-table">
                              {children}
                            </table>
                          </div>
                        ),
                        thead: ({ children }) => (
                          <thead>
                            {children}
                          </thead>
                        ),
                        tbody: ({ children }) => (
                          <tbody>
                            {children}
                          </tbody>
                        ),
                        tr: ({ children }) => (
                          <tr>
                            {children}
                          </tr>
                        ),
                        th: ({ children }) => (
                          <th>
                            {children}
                          </th>
                        ),
                        td: ({ children }) => (
                          <td>
                            {children}
                          </td>
                        ),
                        h1: ({ children }) => (
                          <h1 className="text-xl font-bold text-gray-900 mb-3">
                            {children}
                          </h1>
                        ),
                        h2: ({ children }) => (
                          <h2 className="text-lg font-semibold text-gray-900 mb-2">
                            {children}
                          </h2>
                        ),
                        h3: ({ children }) => (
                          <h3 className="text-md font-medium text-gray-900 mb-2">
                            {children}
                          </h3>
                        ),
                        p: ({ children }) => (
                          <p className="mb-2 text-gray-800">
                            {children}
                          </p>
                        ),
                        strong: ({ children }) => (
                          <strong className="font-semibold text-gray-900">
                            {children}
                          </strong>
                        ),
                        ul: ({ children }) => (
                          <ul className="list-disc list-outside mb-2 ml-4 space-y-0">
                            {children}
                          </ul>
                        ),
                        ol: ({ children }) => (
                          <ol className="list-decimal list-outside mb-2 ml-4 space-y-0">
                            {children}
                          </ol>
                        ),
                        li: ({ children }) => (
                          <li className="text-gray-800 mb-1">
                            {children}
                          </li>
                        ),
                        code: ({ children, ...props }) => (
                          props.className?.includes('inline') || !props.className ? (
                            <code className="bg-gray-100 text-gray-800 px-1 py-0.5 rounded text-sm font-mono">
                              {children}
                            </code>
                          ) : (
                            <pre className="bg-gray-100 text-gray-800 p-3 rounded-lg overflow-x-auto">
                              <code className="text-sm font-mono">
                                {children}
                              </code>
                            </pre>
                          )
                        ),
                        blockquote: ({ children }) => (
                          <blockquote className="border-l-4 border-blue-500 pl-4 italic text-gray-700 my-2">
                            {children}
                          </blockquote>
                        ),
                      }}
                    >
                      {message.content}
                    </ReactMarkdown>
                  )}
                </div>
                
                {message.portfolioSummary && renderPortfolioSummary(message.portfolioSummary)}
                
                {message.insights && message.insights.length > 0 && (
                  <div className="mt-3">
                    <h4 className="font-medium text-gray-900 mb-2 flex items-center">
                      <Lightbulb className="h-4 w-4 mr-2" />
                      Insights
                    </h4>
                    {message.insights.map(renderInsight)}
                  </div>
                )}
                
                <div className={`text-xs mt-2 flex items-center ${
                  isUser ? 'text-blue-200' : 'text-gray-500'
                }`}>
                  <Clock className="h-3 w-3 mr-1" />
                  {message.timestamp.toLocaleTimeString()}
                  {message.queryType && (
                    <span className="ml-2 bg-gray-200 text-gray-700 px-2 py-0.5 rounded-full">
                      {message.queryType}
                    </span>
                  )}
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className={`flex flex-col h-full bg-white ${className}`}>
      {!isAuthenticated ? (
        // Authentication required state
        <div className="flex-1 flex items-center justify-center p-8">
          <div className="text-center max-w-md">
            <div className="w-20 h-20 bg-blue-100 rounded-full flex items-center justify-center mx-auto mb-6">
              <Bot className="h-10 w-10 text-blue-600" />
            </div>
            <h3 className="text-xl font-semibold text-gray-900 mb-3">
              AI Assistant Ready
            </h3>
            <p className="text-gray-600 mb-6">
              Sign in to access your AI portfolio assistant and get personalized insights about your investments.
            </p>
            <button
              onClick={login}
              className="inline-flex items-center space-x-2 bg-blue-600 text-white px-6 py-3 rounded-xl hover:bg-blue-700 transition-colors font-medium"
            >
              <LogIn className="h-5 w-5" />
              <span>Sign In to Start</span>
            </button>
            <p className="text-sm text-gray-500 mt-4">
              Once authenticated, you can ask questions about your portfolio performance, holdings, and get market insights.
            </p>
          </div>
        </div>
      ) : (
        // Authenticated chat interface
        <>
          {/* Messages */}
          <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-gray-50">
            {chatState.messages.map(renderMessage)}
            <div ref={messagesEndRef} />
          </div>

          {/* Input */}
          <div className="border-t border-gray-200 p-4 bg-white">
            <form onSubmit={handleSubmit} className="flex space-x-3">
              <input
                ref={inputRef}
                type="text"
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                placeholder="Ask me about your portfolio..."
                disabled={chatState.isLoading}
                className="flex-1 border border-gray-300 rounded-xl px-4 py-3 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:bg-gray-100 disabled:cursor-not-allowed text-base"
              />
              <button
                type="submit"
                disabled={chatState.isLoading || !inputValue.trim()}
                className="bg-blue-600 text-white px-6 py-3 rounded-xl hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors font-medium"
              >
                <Send className="h-5 w-5" />
              </button>
            </form>
            
            {chatState.error && (
              <div className="mt-3 text-sm text-red-600 flex items-center justify-between bg-red-50 p-3 rounded-lg border border-red-200">
                <div className="flex items-center">
                  <AlertCircle className="h-4 w-4 mr-2 flex-shrink-0" />
                  <span>{chatState.error}</span>
                </div>
                {(chatState.error.includes('sign in') || chatState.error.includes('Authentication')) && (
                  <button
                    onClick={() => window.location.reload()}
                    className="ml-3 bg-blue-600 text-white px-3 py-1 rounded-md text-xs hover:bg-blue-700 transition-colors"
                  >
                    Refresh Page
                  </button>
                )}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}