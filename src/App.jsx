import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Youtube, 
  Music, 
  FileText, 
  Download, 
  Sparkles, 
  Zap, 
  ChevronRight,
  Clipboard,
  CheckCircle2
} from 'lucide-react';
import { generateScript } from './ShortsGeneratorEngine';

function App() {
  const [blogText, setBlogText] = useState('');
  const [platform, setPlatform] = useState('YouTube_Shorts');
  const [isGenerating, setIsGenerating] = useState(false);
  const [result, setResult] = useState(null);
  const [copied, setCopied] = useState(false);

  const handleGenerate = async () => {
    if (!blogText) return;
    setIsGenerating(true);
    try {
      const data = await generateScript(platform, blogText);
      setResult(data);
    } catch (error) {
      console.error(error);
    } finally {
      setIsGenerating(false);
    }
  };

  const copyToClipboard = (text) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const downloadMetadata = (content, filename) => {
    const element = document.createElement("a");
    const file = new Blob([content], {type: 'text/plain'});
    element.href = URL.createObjectURL(file);
    element.download = filename;
    document.body.appendChild(element);
    element.click();
  };

  return (
    <div className="app-container">
      <header>
        <motion.div 
          initial={{ opacity: 0, y: -20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5 }}
        >
          <h1>Shorts Video Generator <span className="badge">PRO</span></h1>
          <p className="subtitle">ブログ記事からバイラル動画スクリプトとAI音楽プロンプトを秒速生成</p>
        </motion.div>
      </header>

      <main>
        <div className="grid">
          {/* Input Panel */}
          <motion.section 
            className="glass glass-card input-panel"
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: 0.2 }}
          >
            <div className="section-header">
              <Sparkles className="icon" size={20} color="#a78bfa" />
              <h2>Generate Script</h2>
            </div>
            
            <div className="form-group">
              <label>Blog Text / Article Content</label>
              <textarea 
                placeholder="ここにブログ記事のテキストを入力..."
                rows="10"
                value={blogText}
                onChange={(e) => setBlogText(e.target.value)}
              />
            </div>

            <div className="form-row">
              <div className="form-group">
                <label>Target Platform</label>
                <div className="platform-toggle">
                  <button 
                    className={`platform-btn ${platform === 'YouTube_Shorts' ? 'active' : ''}`}
                    onClick={() => setPlatform('YouTube_Shorts')}
                  >
                    <Youtube size={18} />
                    YouTube Shorts
                  </button>
                  <button 
                    className={`platform-btn ${platform === 'TikTok' ? 'active' : ''}`}
                    onClick={() => setPlatform('TikTok')}
                  >
                    <Zap size={18} />
                    TikTok
                  </button>
                </div>
              </div>
            </div>

            <button 
              className="btn-primary full-width" 
              onClick={handleGenerate}
              disabled={isGenerating || !blogText}
            >
              {isGenerating ? (
                <span className="loader-text">分析中...</span>
              ) : (
                <>生成を開始する <ChevronRight size={18} /></>
              )}
            </button>
          </motion.section>

          {/* Result Panel */}
          <div className="result-container">
            <AnimatePresence mode="wait">
              {!result ? (
                <motion.div 
                  key="placeholder"
                  className="glass glass-card placeholder-panel"
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                >
                  <Zap className="placeholder-icon" size={48} />
                  <p>記事を入力して生成ボタンを押すと、<br />2パターンのスクリプトとBGM案が表示されます。</p>
                </motion.div>
              ) : (
                <motion.div 
                  key="result"
                  className="result-content"
                  initial={{ opacity: 0, scale: 0.95 }}
                  animate={{ opacity: 1, scale: 1 }}
                >
                  {result.variations.map((v) => (
                    <div key={v.variation_id} className="variation-card glass glass-card">
                      <div className="variation-header">
                        <h3>Variation {v.variation_id} <span className="tag">{v.hook_type} Hook</span></h3>
                        <div className="duration-tag">{v.total_duration}s</div>
                      </div>

                      <div className="bgm-section">
                        <div className="bgm-header">
                          <Music size={18} />
                          <h4>AI Music Prompt</h4>
                        </div>
                        <div className="prompt-box">
                          <code>{v.bgm.auto_generation_prompt}</code>
                          <button 
                            className="btn-icon" 
                            onClick={() => copyToClipboard(v.bgm.auto_generation_prompt)}
                            title="プロンプトをコピー"
                          >
                            {copied ? <CheckCircle2 size={16} color="#10b981" /> : <Clipboard size={16} />}
                          </button>
                        </div>
                        <div className="bgm-actions">
                          <button className="btn-small" onClick={() => downloadMetadata(v.bgm.metadata_txt_content, `metadata_${v.variation_id}.txt`)}>
                            <FileText size={14} /> メタデータ保存
                          </button>
                          <div className="filename-suggestion">
                            <Download size={14} /> {v.bgm.mp3_filename_suggestion}
                          </div>
                        </div>
                      </div>

                      <div className="scenes-list">
                        <h4>Scenes ({v.scenes.length})</h4>
                        {v.scenes.map(scene => (
                          <div key={scene.id} className="scene-item">
                            <div className="scene-number">{scene.id}</div>
                            <div className="scene-details">
                              <p className="narration">"{scene.narration}"</p>
                              <div className="telop-preview" data-style={scene.telop.style}>
                                {scene.telop.text}
                              </div>
                              <div className="scene-meta">
                                <span>{scene.visual}</span>
                                <span className="sync-tag">{scene.music_sync}</span>
                              </div>
                            </div>
                            <div className="scene-duration">{scene.duration}s</div>
                          </div>
                        ))}
                      </div>
                    </div>
                  ))}
                  
                  <div className="json-output glass glass-card">
                    <div className="section-header">
                      <FileText size={18} />
                      <h4>Raw JSON Output</h4>
                    </div>
                    <pre>{JSON.stringify(result, null, 2)}</pre>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </div>
      </main>
    </div>
  );
}

export default App;
