
# NewAPI 管理API文档

## 概述

NewAPI 是下一代大模型网关与AI资产管理系统，基于One API二次开发。本API文档提供了通过编程方式访问和管理NewAPI实例的接口说明。

### 主要功能
- 查询账户余额和用量
- 管理API令牌
- 查看模型列表和渠道信息
- 获取使用统计
- 用户和分组管理

### 基础信息
- **API根路径**: `https://your-newapi-instance.com`
- **数据格式**: JSON
- **认证方式**: Bearer Token (系统访问令牌)
- **兼容性**: 与One API Management API完全兼容

---

## 认证方式

### 获取系统访问令牌

1. 登录NewAPI管理界面
2. 进入「个人设置 → 账户管理 → 安全设置 → 系统访问令牌」
3. 点击「创建新令牌」并复制生成的令牌

### 使用令牌

在所有API请求的header中包含：
```
Authorization: Bearer <your-access-token>
```

---

## API端点参考

### 1. 查询账户信息

**GET** `/api/user/self`

获取当前登录用户的详细信息，包括余额、使用情况等。

#### 参数
无

#### 响应示例
```json
{
  "data": {
    "id": 1,
    "username": "admin",
    "quota": 100.00,
    "used_quota": 45.50,
    "remain_quota": 54.50,
    "group_id": 1,
    "group_name": "默认分组"
  }
}
```

#### 字段说明
| 字段 | 类型 | 说明 |
|------|------|------|
| `quota` | number | 总配额/余额 |
| `used_quota` | number | 已使用配额 |
| `remain_quota` | number | 剩余配额 |
| `group_id` | integer | 用户分组ID |
| `group_name` | string | 分组名称 |

---

### 2. 获取API令牌列表

**GET** `/api/token/`

获取所有API令牌及其使用情况。

#### 参数
| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `p` | integer | 否 | 0 | 页码（从0开始） |
| `size` | integer | 否 | 20 | 每页数量 |
| `name` | string | 否 | - | 按名称搜索 |

#### 响应示例
```json
{
  "total": 10,
  "page": 0,
  "size": 20,
  "data": [
    {
      "id": 1,
      "name": "production",
      "key": "sk-reHR**********OspA",
      "quota": 100.00,
      "used_quota": 45.50,
      "remain_quota": 54.50,
      "status": true,
      "created_time": "2025-01-15T10:30:00Z"
    }
  ]
}
```

---

### 3. 创建新API令牌

**POST** `/api/token/`

创建新的API密钥。

#### 请求体
```json
{
  "name": "my-token",
  "group_id": 1,
  "quota": 50.00
}
```

#### 参数说明
| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `name` | string | 是 | 令牌名称 |
| `group_id` | integer | 否 | 分组ID，默认为1 |
| `quota` | number | 否 | 配额限制，不设置则继承分组设置 |

#### 响应示例
```json
{
  "id": 2,
  "name": "my-token",
  "key": "sk-abcdefghijklmnopqrstuvwxyz123456",
  "quota": 50.00,
  "group_id": 1
}
```
> **安全提示**: 这是唯一显示完整密钥的时机，请妥善保存。

---

### 4. 更新API令牌

**PUT** `/api/token/{id}`

更新现有令牌信息。

#### 请求体
```json
{
  "name": "new-name",
  "quota": 80.00
}
```

---

### 5. 删除API令牌

**DELETE** `/api/token/{id}`

删除指定的API令牌。

---

### 6. 查看模型列表

**GET** `/api/model/`

获取所有可用的AI模型列表。

#### 参数
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `group_id` | integer | 否 | 按分组过滤模型 |
| `show` | string | 否 | 模型显示设置（all/available） |

#### 响应示例
```json
{
  "data": [
    {
      "id": 1,
      "name": "gpt-3.5-turbo",
      "nickname": "GPT-3.5 Turbo",
      "group": "OpenAI",
      "price": 0.002,
      "max_context": 4096
    }
  ]
}
```

---

### 7. 查看渠道列表

**GET** `/api/channel/`

获取所有已配置的渠道信息。

#### 响应示例
```json
{
  "data": [
    {
      "id": 1,
      "name": "OpenAI API",
      "type": "openai",
      "status": true,
      "balance": 100.00
    }
  ]
}
```

---

### 8. 获取用户分组信息

**GET** `/api/group/`

获取所有用户分组及其配置。

#### 响应示例
```json
{
  "data": [
    {
      "id": 1,
      "name": "默认分组",
      "quota": 100.00,
      "used_quota": 150.50,
      "ratio": 1.0,
      "models": ["gpt-3.5-turbo", "gpt-4"]
    }
  ]
}
```

---

### 9. 查询使用统计

**GET** `/api/usage/`

获取API使用情况统计。

#### 参数
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `start_date` | string | 否 | 开始日期 (YYYY-MM-DD) |
| `end_date` | string | 否 | 结束日期 (YYYY-MM-DD) |
| `token_id` | integer | 否 | 按令牌过滤 |

#### 响应示例
```json
{
  "total_requests": 1000,
  "total_tokens": 50000,
  "total_cost": 10.50,
  "daily_usage": [
    {
      "date": "2025-01-15",
      "requests": 100,
      "tokens": 5000,
      "cost": 1.05
    }
  ]
}
```

---

## 完整示例

### Python示例

```python
import requests
import os
from typing import Dict, List

class NewAPIClient:
    """NewAPI管理API客户端"""
    
    def __init__(self, base_url: str, access_token: str, user_id: int = 1):
        """
        初始化客户端
        
        Args:
            base_url: NewAPI实例地址，如 https://api.example.com
            access_token: 系统访问令牌
            user_id: 用户ID，默认为1
        """
        self.base_url = base_url.rstrip('/')
        self.access_token = access_token
        self.user_id = user_id
        self.headers = {
            'Authorization': f'Bearer {access_token}',
            'Content-Type': 'application/json'
        }
    
    def get_balance(self) -> Dict:
        """获取账户余额和用量信息"""
        response = requests.get(
            f'{self.base_url}/api/user/self',
            headers=self.headers
        )
        response.raise_for_status()
        return response.json()
    
    def get_tokens(self, page: int = 0, page_size: int = 20) -> Dict:
        """获取API令牌列表"""
        response = requests.get(
            f'{self.base_url}/api/token/',
            headers=self.headers,
            params={'p': page, 'size': page_size}
        )
        response.raise_for_status()
        return response.json()
    
    def create_token(self, name: str, group_id: int = 1, quota: float = None) -> Dict:
        """创建新的API令牌"""
        payload = {
            "name": name,
            "group_id": group_id
        }
        if quota is not None:
            payload["quota"] = quota
        
        response = requests.post(
            f'{self.base_url}/api/token/',
            headers=self.headers,
            json=payload
        )
        response.raise_for_status()
        return response.json()
    
    def update_token(self, token_id: int, name: str = None, quota: float = None) -> Dict:
        """更新API令牌"""
        payload = {}
        if name:
            payload["name"] = name
        if quota is not None:
            payload["quota"] = quota
        
        response = requests.put(
            f'{self.base_url}/api/token/{token_id}',
            headers=self.headers,
            json=payload
        )
        response.raise_for_status()
        return response.json()
    
    def delete_token(self, token_id: int) -> bool:
        """删除API令牌"""
        response = requests.delete(
            f'{self.base_url}/api/token/{token_id}',
            headers=self.headers
        )
        response.raise_for_status()
        return response.status_code == 200
    
    def get_models(self, group_id: int = None) -> Dict:
        """获取模型列表"""
        params = {}
        if group_id:
            params['group_id'] = group_id
        
        response = requests.get(
            f'{self.base_url}/api/model/',
            headers=self.headers,
            params=params
        )
        response.raise_for_status()
        return response.json()
    
    def get_channels(self) -> Dict:
        """获取渠道列表"""
        response = requests.get(
            f'{self.base_url}/api/channel/',
            headers=self.headers
        )
        response.raise_for_status()
        return response.json()
    
    def get_usage(
        self, 
        start_date: str = None, 
        end_date: str = None, 
        token_id: int = None
    ) -> Dict:
        """获取使用统计"""
        params = {}
        if start_date:
            params['start_date'] = start_date
        if end_date:
            params['end_date'] = end_date
        if token_id:
            params['token_id'] = token_id
        
        response = requests.get(
            f'{self.base_url}/api/usage/',
            headers=self.headers,
            params=params
        )
        response.raise_for_status()
        return response.json()


# 使用示例
if __name__ == "__main__":
    # 从环境变量读取配置（推荐）
    NEWAPI_BASE_URL = os.getenv('NEWAPI_BASE_URL', 'https://your-newapi-instance.com')
    NEWAPI_ACCESS_TOKEN = os.getenv('NEWAPI_ACCESS_TOKEN')
    
    if not NEWAPI_ACCESS_TOKEN:
        raise ValueError("请设置环境变量 NEWAPI_ACCESS_TOKEN")
    
    client = NewAPIClient(
        base_url=NEWAPI_BASE_URL,
        access_tokenNEWAPI_ACCESS_TOKEN
    )
    
    # 1. 查询余额
    balance = client.get_balance()
    print(f"账户余额: {balance['data']['quota']}")
    print(f"已使用: {balance['data']['used_quota']}")
    print(f"剩余配额: {balance['data']['remain_quota']}")
    
    # 2. 查看令牌列表
    tokens = client.get_tokens()
    print(f"\n令牌总数: {tokens['total']}")
    for token in tokens['data']:
        print(f"  - {token['name']}: 已用 {token['used_quota']} / {token['quota']}")
    
    # 3. 查看可用模型
    models = client.get_models()
    print(f"\n可用模型数量: {len(models['data'])}")
    for model in models['data'][:5]:  # 只显示前5个
        print(f"  - {model['name']} ({model['nickname']})")
```

---

### Shell/Bash示例

```bash
#!/bin/bash

# 设置环境变量
export NEWAPI_BASE_URL="https://your-newapi-instance.com"
export NEWAPI_ACCESS_TOKEN="your-access-token"

# 查询余额
echo "=== 账户余额信息 ==="
curl -s -H "Authorization: Bearer $NEWAPI_ACCESS_TOKEN" \
     "$NEWAPI_BASE_URL/api/user/self" | jq '.data'

# 查看令牌列表
echo -e "\n=== API令牌列表 ==="
curl -s -H "Authorization: Bearer $NEWAPI_ACCESS_TOKEN" \
     "$NEWAPI_BASE_URL/api/token/?size=10" | jq '.data[] | {id, name, used_quota, quota}'

# 查看模型列表
echo -e "\n=== 可用模型列表 ==="
curl -s -H "Authorization: Bearer $NEWAPI_ACCESS_TOKEN" \
     "$NEWAPI_BASE_URL/api/model/" | jq '.data[] | {name, nickname, price}'

# 创建新令牌
echo -e "\n=== 创建新令牌 ==="
curl -s -X POST -H "Authorization: Bearer $NEWAPI_ACCESS_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"name":"my-new-token","group_id":1}' \
     "$NEWAPI_BASE_URL/api/token/" | jq '.'
```

---

## 环境变量配置

推荐在以下位置配置环境变量：

### Linux/macOS
```bash
# ~/.bashrc 或 ~/.zshrc
export NEWAPI_BASE_URL="https://your-newapi-instance.com"
export NEWAPI_ACCESS_TOKEN="your-access-token"
export NEWAPI_USER_ID="1"
```

### Windows
```powershell
# 系统环境变量或PowerShell profile
$env:NEWAPI_BASE_URL="https://your-newapi-instance.com"
$env:NEWAPI_ACCESS_TOKEN="your-access-token"
$env:NEWAPI_USER_ID="1"
```

### .env文件（支持dotenv的项目）
```env
NEWAPI_BASE_URL=https://your-newapi-instance.com
NEWAPI_ACCESS_TOKEN=your-access-token
NEWAPI_USER_ID=1
```

---

## 使用newapi-skills插件

NewAPI官方提供了Skills插件，可在支持的AI编辑器中直接调用：

### 安装
```bash
npx skills add https://github.com/QuantumNous/skills --skill newapi
```

### 支持的AI编辑器
- Claude Code
- OpenClaw（龙虾）
- Cursor
- Windsurf
- Cline
- Codex CLI

### 可用指令
| 指令 | 说明 |
|------|------|
| `/newapi models` | 列出可用AI模型 |
| `/newapi groups` | 列出用户分组 |
| `/newapi balance` | 查看账户余额 |
| `/newapi tokens` | 列出API令牌 |
| `/newapi create-token [name]` | 创建新API令牌 |
| `/newapi switch-group` | 切换令牌分组 |
| `/newapi copy-token` | 安全复制密钥到剪贴板 |
| `/newapi apply-token` | 注入密钥到配置文件 |
| `/newapi help` | 提问关于New API的问题 |

---

## 错误处理

API返回标准的HTTP状态码：

| 状态码 | 说明 |
|--------|------|
| 200 | 成功 |
| 400 | 请求参数错误 |
| 401 | 未授权（令牌无效或缺失） |
| 403 | 权限不足 |
| 404 | 资源不存在 |
| 500 | 服务器内部错误 |

### 错误响应格式
```json
{
  "code": 401,
  "msg": "无效的访问令牌",
  "data": null
}
```

---

## 安全建议

1. **保护访问令牌**：系统访问令牌具有管理权限，切勿提交到版本控制系统
2. **使用最小权限原则**：为不同用途创建不同的令牌，并限制配额
3. **定期轮换令牌**：定期更新访问令牌，特别是发生泄露时
4. **启用HTTPS**：API请求必须通过HTTPS进行
5. **审计日志**：定期检查API调用日志，监控异常活动

---

## 兼容性说明

- NewAPI管理API与One API完全兼容
- 建议使用最新的NewAPI版本以获得最佳体验
- 如有API变更，请参考[官方文档](https://docs.newapi.pro/zh/docs/api)

---

## 参考资源

- **官方文档**: https://docs.newapi.pro/zh/docs/api
- **GitHub仓库**: https://github.com/QuantumNous/new-api
- **NewAPI官网**: https://www.newapi.ai
- **One API项目**: https://github.com/songquanpeng/one-api
- **All API Hub**: https://github.com/qixing-jk/all-api-hub

---

## 常见问题

### Q: 如何查看某个特定API密钥的使用情况？
A: 使用`GET /api/token/{id}`端点获取指定令牌的详细信息。

### Q: 如何批量导出所有API密钥？
A: 可以通过调用`/api/token/`获取所有令牌，然后导出为CSV或JSON格式。

### Q: 支持Webhook或回调通知吗？
A: 当前版本不支持Webhook，但可以通过定期调用usage API实现类似功能。

### Q: 如何实现多账户管理？
A: 为每个账户创建独立的系统访问令牌，分别调用API即可。

---

*文档基于NewAPI最新版本编写，如遇API变更请以官方文档为准。最后更新时间：2025年1月*